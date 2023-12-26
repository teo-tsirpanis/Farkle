// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using BitCollections;
using Farkle.Grammars;
using Farkle.Grammars.Writers;
using System.Collections.Immutable;
using System.Diagnostics;
using TerminalSymbol = (Farkle.Builder.Regex Regex, Farkle.Grammars.TokenSymbolHandle Symbol, string Name);

namespace Farkle.Builder.StateMachines;

/// <summary>
/// Contains the logic for building a DFA from a set of regular expressions.
/// Builds a DFA from a set of regular expressions.
/// </summary>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Currently only <see cref="char"/> is supported, with
/// <see cref="byte"/> planned to be supported in the future.</typeparam>
/// <remarks>
/// The algorithm is a substantially modified edition of the one found at §3.9.5 in
/// "Compilers: Principles, Techniques and Tools" by Aho, Lam, Sethi &amp; Ullman.
/// </remarks>
internal readonly struct DfaBuild<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private readonly IReadOnlyCollection<TerminalSymbol> Regexes { get; }

    private readonly CancellationToken CancellationToken { get; }

    private const int TerminalPriority = 1;

    private const int LiteralPriority = 0;

    private static bool IsRegexChars(Regex regex, out ImmutableArray<(TChar, TChar)> ranges, out bool isInverted)
    {
        if (typeof(TChar) == typeof(char))
        {
            if (regex.IsChars(out var chars_, out isInverted))
            {
                ranges = (ImmutableArray<(TChar, TChar)>)(object)chars_;
                return true;
            }
        }
        ranges = default;
        isInverted = false;
        return false;
    }

    public DfaBuild(IReadOnlyCollection<TerminalSymbol> regexes, CancellationToken cancellationToken = default)
    {
        if (typeof(TChar) != typeof(char))
        {
            throw new NotSupportedException("Unsupported character type. Currently only char is supported.");
        }

        Regexes = regexes;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Builds a DFA from a set of regular expressions.
    /// </summary>
    /// <param name="regexes">A collection of tuples of <see cref="Regex"/>es,
    /// their accept <see cref="TokenSymbolHandle"/> and the symbol's name.</param>
    /// <param name="writer">The <see cref="DfaWriter{TChar}"/> to write the DFA to.</param>
    /// <param name="caseSensitive">Whether the DFA will match characters case-sensitively.</param>
    /// <param name="prioritizeFixedLengthSymbols">Whether symbols with fixed-length regexes
    /// will be prioritized in cases of conflicts.</param>
    /// <param name="maxTokenizerStates">The value of <see cref="BuilderOptions.MaxTokenizerStates"/>.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static void Build(IReadOnlyCollection<TerminalSymbol> regexes, DfaWriter<TChar> writer, bool caseSensitive = false,
        bool prioritizeFixedLengthSymbols = true, int maxTokenizerStates = -1, CancellationToken cancellationToken = default)
    {
        var @this = new DfaBuild<TChar>(regexes, cancellationToken);
        var (leaves, followPos, rootFirstPos) = @this.BuildRegexTree(caseSensitive);
    }

    private static Regex LowerRegex(Regex regex, bool caseSensitive, Dictionary<(Regex, bool CaseSensitive), Regex> loweredRegexCache)
    {
        if (loweredRegexCache.TryGetValue((regex, caseSensitive), out var lowered))
        {
            return lowered;
        }

        if (typeof(TChar) == typeof(char))
        {
            Regex result;
            if (regex.IsStringLiteral(out var stringLiteral))
            {
                var builder = ImmutableArray.CreateBuilder<Regex>(stringLiteral.Length);
                foreach (var c in stringLiteral)
                {
                    ImmutableArray<(char, char)> ranges;
                    if (caseSensitive)
                    {
                        ranges = [(c, c)];
                    }
                    else
                    {
                        ranges = RegexRangeCanonicalizer.Canonicalize([(c, c)], false);
                    }

                    builder.Add(Regex.OneOf(ranges));
                }
                result = Regex.Join(builder.MoveToImmutable());
            }
            else if (regex.IsChars(out var chars, out bool isInverted))
            {
                if (caseSensitive && RegexRangeCanonicalizer.IsCanonical(chars.AsSpan()))
                {
                    result = regex;
                }
                else
                {
                    var loweredChars = RegexRangeCanonicalizer.Canonicalize(chars.AsSpan(), caseSensitive);
                    result = isInverted ? Regex.NotOneOf(loweredChars) : Regex.OneOf(loweredChars);
                }
            }
            else
            {
                result = regex;
            }
            loweredRegexCache[(regex, caseSensitive)] = result;
            return result;
        }

        // We should not be reaching this point; the constructor would have thrown.
        return null!;
    }

    private (List<RegexLeaf> Leaves, List<BitSet> FollowPos, BitSet RootFirstPos) BuildRegexTree(bool caseSensitive)
    {
        Dictionary<(Regex, bool CaseSensitive), Regex> loweredRegexCache = [];
        List<RegexLeaf> leaves = [];
        List<BitSet> followPos = [];
        BitSet rootFirstPos = BitSet.Empty;

        foreach (var (regex, symbol, name) in Regexes)
        {
            // If the symbol's regex's root is an Alt, we assign each of its children a different priority. This
            // emulates the behavior of GOLD Parser and resolves some nasty indistinguishable symbols errors.
            // Earlier versions of Farkle were flattening nested Alts. Because we are not doing that anymore,
            // this will slightly change behavior, but the impact is so small that it's not worth proactively
            // caring about.
            ReadOnlySpan<Regex> regexes = regex.IsAlt(out var altRegexes) ? altRegexes.AsSpan() : [regex];
            // The regex contains Regex.Void somewhere.
            bool hasVoid = regexes.IsEmpty;
            // The entire regex is equivalent to Regex.Void and impossible to match.
            // We detect that by checking if its LastPos is empty, which would make
            // it unable to flow to the end leaf.
            bool isVoid = true;
            foreach (var r in regexes)
            {
                var visitResult = Visit(in this, r, caseSensitive);
                rootFirstPos = BitSet.Union(in rootFirstPos, in visitResult.FirstPos);
                // TODO: Avoid adding an end leaf for the same symbol many times.
                int leafIndex = AddLeaf(new RegexLeaf.End(symbol, visitResult.HasStar ? TerminalPriority : LiteralPriority));
                LinkFollowPos(in visitResult.LastPos, BitSet.Singleton(leafIndex));
                hasVoid |= visitResult.HasVoid;
                isVoid &= !visitResult.LastPos.IsEmpty;
            }
            Debug.Assert(!isVoid || hasVoid, "Internal error: isVoid => hasVoid does not hold.");
            if (isVoid)
            {
                // TODO: Warn that the regex for symbol <name> cannot be matched.
            }
            else if (hasVoid)
            {
                // TODO: Warn that part of the regex for symbol <name> cannot be matched.
            }
        }

        return (leaves, followPos, rootFirstPos);

        VisitResult Visit(in DfaBuild<TChar> @this, Regex regex, bool caseSensitive, bool isLowered = false)
        {
            @this.CancellationToken.ThrowIfCancellationRequested();

            bool isCaseOverriden = false;
            caseSensitive = regex.AdjustCaseSensitivityFlag(caseSensitive, ref isCaseOverriden);

            while (regex.IsRegexString(out RegexStringHolder? regexString))
            {
                regex = regexString.GetRegex();
                caseSensitive = regex.AdjustCaseSensitivityFlag(caseSensitive, ref isCaseOverriden);
            }

            if (regex.IsConcat(out ImmutableArray<Regex> regexes))
            {
                VisitResult result = VisitResult.Empty;
                foreach (var r in regexes)
                {
                    VisitResult nextResult = Visit(in @this, r, caseSensitive, isLowered);
                    LinkFollowPos(in result.LastPos, in nextResult.FirstPos);
                    result += nextResult;
                }
                return result;
            }

            if (regex.IsAlt(out regexes))
            {
                if (regexes.IsEmpty)
                {
                    return VisitResult.Void;
                }
                VisitResult result = Visit(in @this, regexes[0], caseSensitive, isLowered);
                foreach (var r in regexes.AsSpan()[1..])
                {
                    result |= Visit(in @this, r, caseSensitive, isLowered);
                }
                return result;
            }

            if (regex.IsLoop(out Regex? loopItem, out int m, out int n))
            {
                VisitResult result = VisitResult.Empty;
                for (int i = 0; i < m; i++)
                {
                    VisitResult nextResult = Visit(in @this, loopItem, caseSensitive, isLowered);
                    LinkFollowPos(in result.LastPos, in nextResult.FirstPos);
                    result += nextResult;
                }

                if (n == int.MaxValue)
                {
                    VisitResult starResult = Visit(in @this, loopItem, caseSensitive, isLowered).AsStar();
                    LinkFollowPos(in starResult.LastPos, in starResult.FirstPos);
                    LinkFollowPos(in result.LastPos, in starResult.FirstPos);
                    result += starResult;
                }
                else
                {
                    for (int i = m; i < n; i++)
                    {
                        VisitResult nextResult = Visit(in @this, loopItem, caseSensitive, isLowered).AsOptional();
                        LinkFollowPos(in result.LastPos, in nextResult.FirstPos);
                        result += nextResult;
                    }
                }
                return result;
            }

            if (!isLowered)
            {
                regex = LowerRegex(regex, caseSensitive, loweredRegexCache);
            }

            if (regex.IsAny())
            {
                return VisitResult.Singleton(AddLeaf(RegexLeaf.Any.Instance));
            }

            if (IsRegexChars(regex, out var chars, out bool isInverted))
            {
                return VisitResult.Singleton(AddLeaf(new RegexLeaf.Chars(chars, isInverted)));
            }

            if (!isLowered)
            {
                return Visit(in @this, regex, caseSensitive, isLowered: true);
            }

            throw new InvalidOperationException("Internal error: unrecognized form of lowered regex.");
        }

        int AddLeaf(RegexLeaf leaf)
        {
            leaves.Add(leaf);
            followPos.Add(BitSet.Empty);
            return leaves.Count - 1;
        }

        void LinkFollowPos(in BitSet source, in BitSet destination)
        {
            foreach (var i in source)
            {
                followPos[i] = BitSet.Union(followPos[i], in destination);
            }
        }
    }

    private abstract class RegexLeaf
    {
        public sealed class Any : RegexLeaf
        {
            private Any() { }

            public static Any Instance { get; } = new();
        }

        public sealed class Chars(ImmutableArray<(TChar Start, TChar End)> ranges, bool isInverted) : RegexLeaf
        {
            public ImmutableArray<(TChar Start, TChar End)> Ranges { get; } = ranges;

            public bool IsInverted { get; } = isInverted;
        }

        public sealed class End(TokenSymbolHandle symbol, int priority) : RegexLeaf
        {
            public TokenSymbolHandle Symbol { get; } = symbol;

            public int Priority { get; } = priority;
        }
    }

    private readonly struct VisitResult(BitSet FirstPos, BitSet LastPos, bool IsNullable,
        RegexCharacteristics Characteristics = RegexCharacteristics.None)
    {
        public readonly BitSet FirstPos = FirstPos;

        public readonly BitSet LastPos = LastPos;

        public bool IsNullable { get; } = IsNullable;

        private RegexCharacteristics Characteristics { get; } = Characteristics;

        public bool HasStar => (Characteristics & RegexCharacteristics.HasStar) != 0;

        public bool HasVoid => (Characteristics & RegexCharacteristics.HasVoid) != 0;

        public VisitResult AsOptional() =>
            new(FirstPos, LastPos, true, Characteristics);

        public VisitResult AsStar() =>
            new(FirstPos, LastPos, true, Characteristics | RegexCharacteristics.HasStar);

        public static VisitResult Empty => new(BitSet.Empty, BitSet.Empty, true);

        public static VisitResult Void => new(BitSet.Empty, BitSet.Empty, false, RegexCharacteristics.HasVoid);

        public static VisitResult Singleton(int index)
        {
            BitSet pos = BitSet.Singleton(index);
            return new VisitResult(pos, pos, IsNullable: false);
        }

        public static VisitResult operator +(in VisitResult left, in VisitResult right)
        {
            return new VisitResult(
                left.IsNullable ? BitSet.Union(in left.FirstPos, in right.FirstPos) : left.FirstPos,
                right.IsNullable ? BitSet.Union(in left.LastPos, in right.LastPos) : right.LastPos,
                left.IsNullable && right.IsNullable,
                left.Characteristics | right.Characteristics);
        }

        public static VisitResult operator |(in VisitResult left, in VisitResult right)
        {
            return new VisitResult(
                BitSet.Union(in left.FirstPos, in right.FirstPos),
                BitSet.Union(in left.LastPos, in right.LastPos),
                left.IsNullable || right.IsNullable,
                left.Characteristics | right.Characteristics);
        }
    }

    /// <summary>
    /// Represents certain interesting characteristics of regexes.
    /// </summary>
    /// <remarks>
    /// Unlike nullability, regex characteristics
    /// must always be combined with a bitwise OR.
    /// </remarks>
    [Flags]
    private enum RegexCharacteristics
    {
        /// <summary>
        /// No characteristics are present.
        /// </summary>
        None = 0,
        /// <summary>
        /// The regex contains a star operator.
        /// </summary>
        HasStar = 1,
        /// <summary>
        /// The regex contains <see cref="Regex.Void"/>.
        /// </summary>
        /// <remarks>
        /// This is usually undesirable. The builder will
        /// emit a warning if the regex of a terminal has
        /// this characteristic.
        /// </remarks>
        HasVoid = 2
    }
}
