// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using BitCollections;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars.Writers;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Farkle.Builder.Dfa;

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
    private readonly IGrammarSymbolsProvider Symbols { get; }

    private readonly CancellationToken CancellationToken { get; }

    private readonly BuilderLogger Log;

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

    private DfaBuild(IGrammarSymbolsProvider symbols, BuilderLogger log, CancellationToken cancellationToken = default)
    {
        if (typeof(TChar) != typeof(char))
        {
            throw new NotSupportedException("Unsupported character type. Currently only char is supported.");
        }

        Symbols = symbols;
        Log = log;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Builds a DFA that matches the symbols of a grammar.
    /// </summary>
    /// <param name="symbols">The symbols of the grammar.</param>
    /// <param name="caseSensitive">Whether the DFA will match characters case-sensitively.</param>
    /// <param name="prioritizeFixedLengthSymbols">Whether symbols with fixed-length regexes
    /// will be prioritized in cases of conflicts.</param>
    /// <param name="maxTokenizerStates">The value of <see cref="BuilderOptions.MaxTokenizerStates"/>.</param>
    /// <param name="log">Used to log events in the building process.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static DfaWriter<TChar>? Build(IGrammarSymbolsProvider symbols, bool caseSensitive = false,
        bool prioritizeFixedLengthSymbols = true, int maxTokenizerStates = -1, BuilderLogger log = default,
        CancellationToken cancellationToken = default)
    {
        var @this = new DfaBuild<TChar>(symbols, log, cancellationToken);
        var (leaves, followPos, rootFirstPos) = @this.BuildRegexTree(caseSensitive);
        maxTokenizerStates = BuilderOptions.GetMaxTokenizerStates(maxTokenizerStates, leaves.Count);
        var dfaStates = @this.BuildDfaStates(leaves, followPos, rootFirstPos, maxTokenizerStates);
        if (dfaStates is null)
        {
            return null;
        }
        return @this.WriteDfa(dfaStates, prioritizeFixedLengthSymbols);
    }

    private static int? FindDominantSymbol(List<(int Priority, int SymbolIndex)> acceptSymbols, bool prioritizeFixedLengthSymbols)
    {
        switch (acceptSymbols)
        {
            case []: return null;
            case [(_, var symbol)]: return symbol;
        }

        acceptSymbols.Sort((x1, x2) => x1.Priority.CompareTo(x2.Priority));

        var (firstPriority, firstSymbol) = acceptSymbols[0];

        for (int i = 1; i < acceptSymbols.Count; i++)
        {
            if (firstSymbol != acceptSymbols[i].SymbolIndex)
            {
                if (prioritizeFixedLengthSymbols && firstPriority < acceptSymbols[i].Priority)
                {
                    return firstSymbol;
                }
                return null;
            }
        }

        // At this point all symbols are the same.
        return firstSymbol;
    }

    private DfaWriter<TChar> WriteDfa(List<DfaState> states, bool prioritizeFixedLengthSymbols)
    {
        DfaWriter<TChar> dfaWriter = new(states.Count);
        HashSet<BitSet>? seenConflicts = null;
        BitArrayNeo? conflictsOfState = null;

        foreach (var state in states)
        {
            foreach (var (start, end, target) in state.Transitions)
            {
                if (target == state.DefaultTransition)
                {
                    continue;
                }
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

            if (FindDominantSymbol(state.AcceptSymbols, prioritizeFixedLengthSymbols) is { } sym)
            {
                dfaWriter.AddAccept(Symbols.GetTokenSymbolHandle(sym));
            }
            else
            {
                // FindDominantSymbol returning null means either:
                // 1. There are no accept symbols so we add nothing.
                // 2. There are multiple accept symbols so we add them all.
                if (state.AcceptSymbols is not [])
                {
                    seenConflicts ??= [];
                    conflictsOfState ??= new BitArrayNeo(Symbols.SymbolCount);
                    conflictsOfState.SetAll(false);
                    var namesBuilder = ImmutableArray.CreateBuilder<string>(state.AcceptSymbols.Count);
                    var symbolInfoBuilder = ImmutableArray.CreateBuilder<(TokenSymbolKind, bool ShouldDisambiguate)>(state.AcceptSymbols.Count);
                    foreach (var (_, symbol) in state.AcceptSymbols)
                    {
                        if (!conflictsOfState.Set(symbol, true))
                        {
                            continue;
                        }
                        var name = Symbols.GetName(symbol);
                        namesBuilder.Add(name.Name);
                        symbolInfoBuilder.Add((name.Kind, name.ShouldDisambiguate));
                    }
                    Debug.Assert(namesBuilder.Count > 1);
                    // Do not log the same set of indistunguishable symbols twice.
                    if (seenConflicts.Add(conflictsOfState.ToBitSet()))
                    {
                        Log.IndistinguishableSymbols(new(namesBuilder.DrainToImmutable(), symbolInfoBuilder.DrainToImmutable()));
                    }
                }
                foreach (var (_, symbol) in state.AcceptSymbols)
                {
                    dfaWriter.AddAccept(Symbols.GetTokenSymbolHandle(symbol));
                }
            }

            dfaWriter.FinishState();
        }

        return dfaWriter;
    }

    private List<DfaState>? BuildDfaStates(List<RegexLeaf> leaves, List<BitSet> followPos, BitSet rootStateId, int maxStates)
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

            if (maxStates < stateList.Count)
            {
                // If the maximum number of states has been reached, do not create a DFA.
                // This is the best option, out of writing the half-built DFA to the grammar
                // and either:
                // 1. Marking the whole grammar as unparsable, which we can't do because the
                //    parser might be otherwise usable.
                // 2. Marking the DFA as with conflicts, which we can't do because it might
                //    not have any conflicts.
                // 3. Introducing a new "untokenizable" grammar flag, which is not a good
                //    idea because it has a very niche use case and it would need additional
                //    flags when we add byte parsers.
                Log.DfaStateLimitExceeded(maxStates);
                return null;
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
                    case RegexLeaf.End { SymbolIndex: int symbolIndex, Priority: int priority }:
                        S.AcceptSymbols.Add((priority, symbolIndex));
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
                        // We must emit an explicit failure if we are inside all the inverted leaves
                        // and only these.
                        // The presence of Any leaves will cause the above to never hold, because
                        // Any leaves are inverted Chars leaves with no ranges, which means that
                        // some inverted leaves will never be entered.
                        bool insideAllInvertedRanges = invertedDepth == invertedCount;
                        bool insideOnlyInvertedRanges = invertedDepth == depth;
                        bool shouldEmitFailure = insideAllInvertedRanges && insideOnlyInvertedRanges;
                        // We are inside all the inverted leaves, and also inside some regular leaves.
                        // We must emit a failure.
                        int? transitionState = shouldEmitFailure ? null : GetOrAddState(FollowLeaves(presentLeaves));
                        S.Transitions.Add((transitionRangeStart, transitionRangeEnd, transitionState));
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
                    chars = RegexRangeCanonicalizer.Canonicalize(chars.AsSpan(), caseSensitive);
                    result = isInverted ? Regex.NotOneOf(chars) : Regex.OneOf(chars);
                }
                // If the regex has been canonicalized into a set of all/none characters
                // and is/isn't inverted, change it to Regex.Void.
                if ((chars, isInverted) is ([], false) or ([(char.MinValue, char.MaxValue)], true))
                {
                    result = Regex.Void;
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

        int count = Symbols.SymbolCount;
        for (int i = 0; i < count; i++)
        {
            Regex regex = Symbols.GetRegex(i);
            // If the symbol's regex's root is an Alt, we assign each of its children a different priority. This
            // emulates the behavior of GOLD Parser and resolves some nasty indistinguishable symbols errors.
            // Earlier versions of Farkle were flattening nested Alts. Because we are not doing that anymore,
            // this will slightly change behavior, but the impact is so small that it's not worth proactively
            // caring about.
            ReadOnlySpan<Regex> regexes = regex.IsAlt(out var altRegexes) ? altRegexes.AsSpan() : [regex];
            // The regex contains Regex.Void somewhere.
            bool hasVoid = regexes.IsEmpty;
            // The entire regex is equivalent to Regex.Void and impossible to match.
            // We detect that by checking if it's not nullable or its LastPos is empty,
            // which would make it unable to flow from the root to the end leaf.
            bool isVoid = true;
            int? endLeafIndexTerminal = null, endLeafIndexLiteral = null;
            foreach (var r in regexes)
            {
                var info = Visit(in this, i, r, caseSensitive);
                rootFirstPos = BitSet.Union(in rootFirstPos, in info.FirstPos);
                int leafIndex = info.HasStar
                    ? endLeafIndexTerminal ??= AddLeaf(new RegexLeaf.End(i, TerminalPriority))
                    : endLeafIndexLiteral ??= AddLeaf(new RegexLeaf.End(i, LiteralPriority));
                if (info.IsNullable)
                {
                    rootFirstPos = rootFirstPos.Set(leafIndex, true);
                }
                LinkFollowPos(in info.LastPos, BitSet.Singleton(leafIndex));
                hasVoid |= info.HasVoid;
                isVoid &= info.IsVoid;
            }
            Debug.Assert(!isVoid || hasVoid, "Internal error: isVoid => hasVoid does not hold.");
            // Let's emit the same diagnostic for a regex that both is entirely void
            // or part of it is. This situation is very niche.
            if (Log.IsEnabled(DiagnosticSeverity.Warning) && isVoid || hasVoid)
            {
                Log.RegexContainsVoid(Symbols.GetName(i));
            }
        }

        return (leaves, followPos, rootFirstPos);

        RegexInfo Visit(in DfaBuild<TChar> @this, int symbolIndex, Regex regex, bool caseSensitive, bool isLowered = false)
        {
            @this.CancellationToken.ThrowIfCancellationRequested();

            bool isCaseOverriden = false;
            caseSensitive = regex.AdjustCaseSensitivityFlag(caseSensitive, ref isCaseOverriden);

            while (regex.IsRegexString(out RegexStringHolder? regexString))
            {
                switch (regexString.GetRegexOrError())
                {
                    case Regex r:
                        regex = r;
                        break;
                    case object error:
                        // If a faulty string regex exists many times in the grammar (or just once, but `Repeat`ed), we
                        // will log the same error multiple times. This is also the behavior in previous versions of Farkle.
                        // We could add checks to ensure the error is logged only once, but it would get quite complicated
                        // for little benefit; the most common usage pattern of string regexes is directly on a terminal,
                        // and not composed in another regex.
                        @this.Log.RegexStringParseError(@this.Symbols.GetName(symbolIndex), error);
                        regex = Regex.Void;
                        break;
                }
                caseSensitive = regex.AdjustCaseSensitivityFlag(caseSensitive, ref isCaseOverriden);
            }

            if (regex.IsConcat(out ImmutableArray<Regex> regexes))
            {
                RegexInfo info = RegexInfo.Empty;
                foreach (var r in regexes)
                {
                    RegexInfo nextResult = Visit(in @this, symbolIndex, r, caseSensitive, isLowered);
                    LinkFollowPos(in info.LastPos, in nextResult.FirstPos);
                    info += nextResult;
                }
                return info;
            }

            if (regex.IsAlt(out regexes))
            {
                RegexInfo info = RegexInfo.Void;
                foreach (var r in regexes)
                {
                    info |= Visit(in @this, symbolIndex, r, caseSensitive, isLowered);
                }
                return info;
            }

            if (regex.IsLoop(out Regex? loopItem, out int m, out int n))
            {
                RegexInfo info = RegexInfo.Empty;
                for (int i = 0; i < m; i++)
                {
                    RegexInfo nextInfo = Visit(in @this, symbolIndex, loopItem, caseSensitive, isLowered);
                    LinkFollowPos(in info.LastPos, in nextInfo.FirstPos);
                    info += nextInfo;
                }

                if (n == int.MaxValue)
                {
                    RegexInfo starInfo = Visit(in @this, symbolIndex, loopItem, caseSensitive, isLowered).AsStar();
                    LinkFollowPos(in starInfo.LastPos, in starInfo.FirstPos);
                    LinkFollowPos(in info.LastPos, in starInfo.FirstPos);
                    info += starInfo;
                }
                else
                {
                    for (int i = m; i < n; i++)
                    {
                        RegexInfo nextInfo = Visit(in @this, symbolIndex, loopItem, caseSensitive, isLowered).AsOptional();
                        LinkFollowPos(in info.LastPos, in nextInfo.FirstPos);
                        info += nextInfo;
                    }
                }
                return info;
            }

            if (!isLowered)
            {
                regex = LowerRegex(regex, caseSensitive, loweredRegexCache);
            }

            if (regex.IsAny())
            {
                return RegexInfo.Singleton(AddLeaf(RegexLeaf.Any));
            }

            if (IsRegexChars(regex, out var chars, out bool isInverted))
            {
                return RegexInfo.Singleton(AddLeaf(new RegexLeaf.Chars(chars, isInverted)));
            }

            if (!isLowered)
            {
                return Visit(in @this, symbolIndex, regex, caseSensitive, isLowered: true);
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

        public List<(int Priority, int SymbolIndex)> AcceptSymbols { get; } = [];

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

        public sealed class End(int symbolIndex, int priority) : RegexLeaf
        {
            public int SymbolIndex { get; } = symbolIndex;

            public int Priority { get; } = priority;
        }
    }

    private readonly struct RegexInfo(BitSet FirstPos, BitSet LastPos, bool IsNullable,
        RegexCharacteristics Characteristics = RegexCharacteristics.None)
    {
        public readonly BitSet FirstPos = FirstPos;

        public readonly BitSet LastPos = LastPos;

        public bool IsNullable { get; } = IsNullable;

        /// <summary>
        /// Whether the regex cannot be followed by any character.
        /// </summary>
        /// <remarks>
        /// This is usually undesirable. The builder will
        /// emit a warning if the regex of a terminal has
        /// this characteristic.
        /// </remarks>
        public bool IsVoid => !IsNullable && LastPos.IsEmpty;

        private RegexCharacteristics Characteristics { get; } = Characteristics;

        public bool HasStar => (Characteristics & RegexCharacteristics.HasStar) != 0;

        public bool HasVoid => (Characteristics & RegexCharacteristics.HasVoid) != 0;

        public RegexInfo AsOptional() =>
            new(FirstPos, LastPos, true, Characteristics);

        public RegexInfo AsStar() =>
            new(FirstPos, LastPos, true, Characteristics | RegexCharacteristics.HasStar);

        public static RegexInfo Empty => new(BitSet.Empty, BitSet.Empty, true);

        public static RegexInfo Void => new(BitSet.Empty, BitSet.Empty, false);

        public static RegexInfo Singleton(int index)
        {
            BitSet pos = BitSet.Singleton(index);
            return new RegexInfo(pos, pos, IsNullable: false);
        }

        public static RegexInfo operator +(in RegexInfo left, in RegexInfo right)
        {
            // We can skip checking if left is void because when processing a concatenation of regexes,
            // left starts to be RegexInfo.Empty and right gets passed all the regexes eventually,
            // so we don't miss anything.
            RegexCharacteristics hasVoidMaybe = right.IsVoid
                ? RegexCharacteristics.HasVoid
                : RegexCharacteristics.None;
            return new RegexInfo(
                left.IsNullable ? BitSet.Union(in left.FirstPos, in right.FirstPos) : left.FirstPos,
                right.IsNullable ? BitSet.Union(in left.LastPos, in right.LastPos) : right.LastPos,
                left.IsNullable && right.IsNullable,
                left.Characteristics | right.Characteristics | hasVoidMaybe);
        }

        public static RegexInfo operator |(in RegexInfo left, in RegexInfo right)
        {
            return new RegexInfo(
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
        /// <para>
        /// This is usually undesirable. The builder will
        /// emit a warning if the regex of a terminal has
        /// this characteristic.
        /// </para>
        /// <para>
        /// This characteristic gets originated when a <see cref="RegexInfo"/>
        /// gets concatenated on the right with one that has the
        /// <see cref="RegexInfo.IsVoid"/> property.
        /// </para>
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
