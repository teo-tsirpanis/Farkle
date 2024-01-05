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

    // Priorities. The lower the number, the higher the priority.

    /// <summary>
    /// The priority number for fixed-size regexes that do
    /// not directly or indirectly contain a star operator.
    /// </summary>
    private const int LiteralPriority = 0;

    /// <summary>
    /// The priority number for regexes that do not fall into
    /// any other category.
    /// </summary>
    private const int TerminalPriority = 1;

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

    private static TChar PreviousChar(TChar c) => (TChar)(object)(char)((char)(object)c - 1);

    private static TChar NextChar(TChar c) => (TChar)(object)(char)((char)(object)c + 1);

    private static TChar MinCharValue => (TChar)(object)char.MinValue;

    private static TChar MaxCharValue => (TChar)(object)char.MaxValue;

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
    /// <param name="caseSensitive">Whether the DFA will match characters case-sensitively.</param>
    /// <param name="prioritizeFixedLengthSymbols">Whether symbols with fixed-length regexes
    /// will be prioritized in cases of conflicts.</param>
    /// <param name="maxTokenizerStates">The value of <see cref="BuilderOptions.MaxTokenizerStates"/>.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static DfaWriter<TChar> Build(IReadOnlyCollection<TerminalSymbol> regexes, bool caseSensitive = false,
        bool prioritizeFixedLengthSymbols = true, int maxTokenizerStates = -1, CancellationToken cancellationToken = default)
    {
        var @this = new DfaBuild<TChar>(regexes, cancellationToken);
        var (leaves, followPos, rootFirstPos) = @this.BuildRegexTree(caseSensitive);
        maxTokenizerStates = BuilderOptions.GetMaxTokenizerStates(maxTokenizerStates, leaves.Count);
        var dfaStates = @this.BuildDfaStates(leaves, followPos, rootFirstPos, maxTokenizerStates);
        return WriteDfa(dfaStates, prioritizeFixedLengthSymbols);
    }

    private static TokenSymbolHandle? FindDominantTokenSymbolHandle(List<(int Priority, TokenSymbolHandle Symbol)> acceptSymbols)
    {
        // This algorithm mimics what Farkle 6 does:
        // 1. Account for the trivial cases of zero or one accept symbols.
        // 2. Sort the accept symbols by priority.
        // 3. Lock on the first list item and loop over the subsequent list items.
        //    * If you see the same symbol next to the first one in the list, ignore it.
        //    * If you see a different symbol with a lower priority, it means that the
        //      first symbol has prevailed. Return it.
        //    * If you see a different symbol with the same priority, goto step 4.
        // 4. Return with a failure to resolve the conflict.
        switch (acceptSymbols)
        {
            case []: return null;
            case [(_, var symbol)]: return symbol;
        }

        acceptSymbols.Sort((x1, x2) => x1.Priority.CompareTo(x2.Priority));

        var (firstPriority, firstSymbol)= acceptSymbols[0];

        for (int i = 1; i < acceptSymbols.Count; i++)
        {
            if (firstSymbol != acceptSymbols[i].Symbol)
            {
                if (firstPriority < acceptSymbols[i].Priority)
                {
                    return firstSymbol;
                }
                break;
            }
        }

        return null;
    }

    private static DfaWriter<TChar> WriteDfa(List<DfaState> states, bool prioritizeFixedLengthSymbols)
    {
        DfaWriter<TChar> dfaWriter = new(states.Count);

        foreach (var state in states)
        {
            foreach (var (start, end, target) in state.Transitions)
            {
                if (target is { } t)
                {
                    dfaWriter.AddEdge(start, end, t);
                }
                else
                {
                    dfaWriter.AddEdgeFail(start, end);
                }
            }

            if (state.DefaultTransition is { } dt)
            {
                dfaWriter.SetDefaultTransition(dt);
            }

            if (prioritizeFixedLengthSymbols && FindDominantTokenSymbolHandle(state.AcceptSymbols) is { } sym)
            {
                dfaWriter.AddAccept(sym);
            }
            else
            {
                // Returning null means either:
                // 1. There are no accept symbols so we add nothing.
                // 2. There are multiple accept symbols so we add them all.
                foreach (var (_, symbol) in state.AcceptSymbols)
                {
                    dfaWriter.AddAccept(symbol);
                }
            }

            dfaWriter.FinishState();
        }

        return dfaWriter;
    }

    private List<DfaState> BuildDfaStates(List<RegexLeaf> leaves, List<BitSet> followPos, BitSet rootStateId, int maxStates)
    {
        Dictionary<BitSet, DfaState> states = [];
        List<DfaState> stateList = [];
        Stack<int> unmarkedStates = [];

        List<(TChar, IntervalType, int)> stateIntervals = [];
        BitArrayNeo presentLeaves = new(leaves.Count);
        List<BitSet> followPosUnionCache = [];

        _ = GetOrAddState(rootStateId);
        while (unmarkedStates.TryPop(out int stateIdx))
        {
            CancellationToken.ThrowIfCancellationRequested();

            if (maxStates == stateList.Count)
            {
                // TODO: Emit a non-throwing error.
                throw new InvalidOperationException("DFA state limit reached.");
            }

            DfaState S = stateList[stateIdx];

            stateIntervals.Clear();
            presentLeaves.SetAll(false);

            bool emitDefaultTransition = false;
            int invertedCount = 0;
            foreach (int i in S.StateId)
            {
                switch (leaves[i])
                {
                    case RegexLeaf.End { Symbol: TokenSymbolHandle symbol, Priority: int priority }:
                        S.AcceptSymbols.Add((priority, symbol));
                        break;
                    case RegexLeaf.Chars x:
                        if (x.IsInverted)
                        {
                            presentLeaves[i] = true;
                            emitDefaultTransition = true;
                            invertedCount++;
                        }
                        foreach (var (start, end) in x.Ranges)
                        {
                            stateIntervals.Add((start, x.IsInverted ? IntervalType.InvertedStart : IntervalType.Start, i));
                            stateIntervals.Add((end, x.IsInverted ? IntervalType.InvertedEnd : IntervalType.End, i));
                        }
                        break;
                }
            }

            stateIntervals.Sort();

            TChar? previousChar = null;
            bool previousIsStart = false;
            int depth = 0;
            int invertedDepth = 0;

            foreach (var (c, type, leaf) in stateIntervals)
            {
                bool isStart = type is IntervalType.Start or IntervalType.InvertedStart;
                // We first see if we should attempt emitting a transition, which is if:
                // 1. We are inside a range (this implies that we have seen a character before).
                // 2. Either:
                //    a. The current character is different than the one seen in the previous iteration.
                //    b. The current character is the same with the one seen in the previous iteration,
                //    but we currently are at the end of a range, while we were at the start of a range
                //    in the previous iteration.
                //        The reason for this is to account for single-character ranges, such as [a-a].
                bool isInsideRange = depth > 0;
                bool characterChanged = previousChar is { } c0 && c0.CompareTo(c) < 0;
                bool intervalTypeChanged = previousIsStart && !isStart;
                bool shouldEmitTransition = isInsideRange && (characterChanged || intervalTypeChanged);
                if (shouldEmitTransition)
                {
                    // Implied by isInsideRange. If the depth is non-zero,
                    // we have surely seen at least one character before.
                    Debug.Assert(previousChar is not null);

                    // We must emit an explicit failure if we are inside all the inverted leaves
                    // and only these.
                    // The presence of Any leaves will cause the above to never hold, because
                    // Any leaves are inverted Chars leaves with no ranges, which means that
                    // some inverted leaves will never be entered.
                    bool insideAllInvertedRanges = invertedDepth == invertedCount;
                    bool insideOnlyInvertedRanges = invertedDepth == depth;
                    bool shouldEmitFailure = insideAllInvertedRanges && insideOnlyInvertedRanges;
                    if (shouldEmitFailure)
                    {
                        // We are inside all the inverted leaves, and also inside some regular leaves.
                        // We must emit a failure.
                        S.Transitions.Add((previousChar.GetValueOrDefault(), c, null));
                    }
                    else
                    {
                        // Adjust the transition range to account for ranges inside other ranges.
                        // If we are inside some range, and saw another range start, the transition
                        // must end at the previous character than the current one.
                        // Similarly, if a range has ended just before, the transition must start
                        // at the next character than the previous one.
                        // For example, if we have the ranges [0-9] and [2-5], we must emit transitions
                        // for [0-1], [2-5] and [6-9] (the first and last should point to the same state).
                        TChar transitionRangeStart = previousChar.GetValueOrDefault();
                        bool previousIsEnd = !previousIsStart;
                        if (previousIsEnd)
                        {
                            // This cannot overflow because previousChar cannot take the maximum
                            // character value and this path be entered at the same time.
                            // A range that is before the last one cannot end at the maximum
                            // character value.
                            transitionRangeStart = NextChar(transitionRangeStart);
                        }
                        TChar transitionRangeEnd = c;
                        if (isStart)
                        {
                            // This cannot underflow because to enter this path, a range must
                            // have already started, and only the first item in the list can
                            // have a NUL character.
                            transitionRangeEnd = PreviousChar(transitionRangeEnd);
                        }

                        // Don't emit a transition if the range start is greater than the range end.
                        // This can occur when we have three leaves with ranges [a-b], [a-a] and [b-b],
                        // causing failures later when writing the DFA.
                        // This if statement fixed this and FsCheck has not reported any other failures.
                        if (transitionRangeStart.CompareTo(transitionRangeEnd) <= 0)
                        {
                            int transitionState = GetOrAddState(FollowLeaves(presentLeaves));
                            S.Transitions.Add((transitionRangeStart, transitionRangeEnd, transitionState));
                        }
                    }
                }

                // Change presentLeaves.
                // The idea is that when a range starts/ends, we add/remove its leaf to/from presentLeaves.
                // Conversely, because inverted leaves are present from the start,
                // when an inverted range starts/ends, we remove/add its leaf from/to presentLeaves.
                // Because we have canonicalized the ranges of each leaf to not overlap,
                // we don't add a leaf to presentLeaves twice, and this essentially means that the value
                // of presentLeaves[leaf] gets flipped when a range starts or ends.
                bool switchValue;
                switch (type)
                {
                    case IntervalType.Start:
                        depth++;
                        switchValue = true;
                        break;
                    case IntervalType.InvertedStart:
                        depth++;
                        invertedDepth++;
                        switchValue = false;
                        break;
                    case IntervalType.End:
                        depth--;
                        switchValue = false;
                        break;
                    default:
                        Debug.Assert(type is IntervalType.InvertedEnd);
                        depth--;
                        invertedDepth--;
                        switchValue = true;
                        break;
                }
                Debug.Assert(presentLeaves[leaf] != switchValue);
                presentLeaves[leaf] = switchValue;
                previousChar = c;
                previousIsStart = isStart;
            }

            Debug.Assert(depth is 0);
            // If there is a transition for every possible character,
            // a default transition will be unreachable so don't emit it.
            if (emitDefaultTransition && !S.IsTransitionSpaceFull())
            {
                // At the end of the interval loop, presentLeaves should contain
                // the indices for the any and inverted character leaves.
                S.DefaultTransition = GetOrAddState(FollowLeaves(presentLeaves));
            }
        }

        return stateList;

        BitSet FollowLeaves(BitArrayNeo presentLeaves)
        {
            followPosUnionCache.Clear();
            foreach (var i in presentLeaves)
            {
                followPosUnionCache.Add(followPos[i]);
            }
            return BitSet.UnionMany(followPosUnionCache);
        }

        int GetOrAddState(BitSet stateId)
        {
            if (states.TryGetValue(stateId, out var state))
            {
                return state.Index;
            }

            int index = stateList.Count;
            state = new DfaState(stateId, index);
            unmarkedStates.Push(index);
            stateList.Add(state);
            states.Add(stateId, state);
            return index;
        }
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
            int? endLeafIndexTerminal = null, endLeafIndexLiteral = null;
            foreach (var r in regexes)
            {
                var visitResult = Visit(in this, r, caseSensitive);
                rootFirstPos = BitSet.Union(in rootFirstPos, in visitResult.FirstPos);
                int leafIndex = visitResult.HasStar
                    ? endLeafIndexTerminal ??= AddLeaf(new RegexLeaf.End(symbol, TerminalPriority))
                    : endLeafIndexLiteral ??= AddLeaf(new RegexLeaf.End(symbol, LiteralPriority));
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
                return VisitResult.Singleton(AddLeaf(RegexLeaf.Any));
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

    private sealed class DfaState(BitSet stateId, int index)
    {
        public BitSet StateId { get; } = stateId;

        public int Index { get; } = index;

        public List<(TChar, TChar, int?)> Transitions { get; } = [];

        public int? DefaultTransition { get; set; }

        public List<(int Priority, TokenSymbolHandle)> AcceptSymbols { get; } = [];

        /// <summary>
        /// Returns whether the transitions of this state cover all
        /// possible values <typeparamref name="TChar"/> can take.
        /// </summary>
        /// <remarks>
        /// This method assumes the items of <see cref="Transitions"/> are sorted.
        /// </remarks>
        public bool IsTransitionSpaceFull()
        {
            TChar lastEnd;
            switch (Transitions)
            {
                case [(var start, var end, _), ..]:
                    if (start.CompareTo(MinCharValue) > 0)
                    {
                        return false;
                    }
                    lastEnd = end;
                    break;
                default: return false;
            }

            for (int i = 1; i < Transitions.Count; i++)
            {
                var (start, end, _) = Transitions[i];
                if (lastEnd.CompareTo(PreviousChar(start)) != 0)
                {
                    return false;
                }
                lastEnd = end;
            }

            return lastEnd.CompareTo(MaxCharValue) == 0;
        }
    }

    private abstract class RegexLeaf
    {
        public static RegexLeaf Any { get; } = new Chars([], true);

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

    private enum IntervalType : byte
    {
        // It is important that the Start values are before the End values.
        Start,
        InvertedStart,
        InvertedEnd,
        End
    }
}
