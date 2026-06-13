// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using BitCollections;
using Farkle.Buffers;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;
using Farkle.Grammars.Writers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

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
/// <param name="symbolNameProvider">An object that provides diagnostic information about token symbols.</param>
/// <param name="cancellationToken">Used to cancel the building process.</param>
/// <param name="log">Used to log events in the building process.</param>
internal struct DfaBuild<TChar>(ISymbolNameProvider symbolNameProvider,
    BuilderLogger log = default, CancellationToken cancellationToken = default)
    where TChar : unmanaged, IComparable<TChar>, IMinMaxValue<TChar>, INumberBase<TChar>
{
    private CancellationToken CancellationToken { get; } = cancellationToken;

    private readonly BuilderLogger Log = log;

    private RegexRangeCanonicalizer ReusableRangeCanonicalizer = new();

    private readonly BuilderSymbolName GetSymbolName(TokenSymbolHandle symbol) =>
        symbol.HasValue ? symbolNameProvider.GetName(symbol) : new("", TokenSymbolKind.Terminal, false);

    // Priorities. The higher the number, the higher the priority.

    /// <summary>
    /// The priority number for regexes of noise symbols.
    /// </summary>
    private const int NoisePriority = int.MinValue;

    /// <summary>
    /// The priority number for regexes that do not fall into
    /// any other category.
    /// </summary>
    private const int TerminalPriority = 0;

    /// <summary>
    /// The priority number for fixed-size regexes that do
    /// not directly or indirectly contain a star operator.
    /// </summary>
    private const int LiteralPriority = 1;

    private static bool IsRegexChars(Regex regex, out ImmutableBuffer<TChar> chars, out Regex.CharsFlags flags)
    {
        if (typeof(TChar) == typeof(char))
        {
            bool result = regex.IsChars(out var c, out flags);
            chars = Unsafe.BitCast<ImmutableBuffer<char>, ImmutableBuffer<TChar>>(c);
            return result;
        }
        ThrowHelpers.ThrowUnsupportedCharacterException();
        throw null;
    }

    private static bool IsRegexCharRanges(Regex regex, out ImmutableArray<(TChar, TChar)> ranges, out Regex.CharsFlags flags)
    {
        if (typeof(TChar) == typeof(char))
        {
            bool result = regex.IsCharRanges(out var r, out flags);
            ranges = Unsafe.BitCast<ImmutableArray<(char, char)>, ImmutableArray<(TChar, TChar)>>(r);
            return result;
        }
        ThrowHelpers.ThrowUnsupportedCharacterException();
        throw null;
    }

    /// <summary>
    /// Builds a DFA that matches a <see cref="Regex"/>.
    /// </summary>
    /// <param name="regex">The regex to build.</param>
    /// <param name="dfaWriter">The <see cref="DfaWriter{TChar}"/> to write the DFA's states to.</param>
    /// <param name="options">Options to customize the building process.</param>
    /// <param name="maxTokenizerStates">The value of <see cref="BuilderOptions.MaxTokenizerStates"/>.</param>
    /// <returns>Whether building succeeded.</returns>
    public bool Build(Regex regex, DfaWriter<TChar> dfaWriter, DfaBuildOptions options = DfaBuildOptions.None,
        int maxTokenizerStates = -1)
    {
        if (typeof(TChar) != typeof(char))
        {
            ThrowHelpers.ThrowUnsupportedCharacterException();
        }

        // If there are no symbols, the algorithm will run normally and produce a DFA
        // with one state and no edges. The alternative would be to produce no DFA at
        // all, but it was rejected because it would set the IsFailing flag in the parser,
        // but there is no failure here; the grammar has no symbols and the builder
        // successfully produces a DFA that will always fail either way.

        var (leaves, followPos, rootFirstPos) = BuildRegexTree(regex, options);
        if (leaves is null)
        {
            return false;
        }
        maxTokenizerStates = BuilderOptions.GetMaxTokenizerStates(maxTokenizerStates, leaves.Count);
        var dfaStates = BuildDfaStates(leaves, followPos, rootFirstPos, maxTokenizerStates);
        if (dfaStates is null)
        {
            return false;
        }
        WriteDfa(dfaStates, dfaWriter, options);
        return true;
    }

    private static TokenSymbolHandle FindDominantSymbol(List<(int Priority, TokenSymbolHandle Symbol)> acceptSymbols, DfaBuildOptions options)
    {
        switch (acceptSymbols)
        {
            case []: return default;
            case [(_, var symbol)]: return symbol;
        }

        acceptSymbols.Sort(static (x1, x2) => -x1.Priority.CompareTo(x2.Priority));

        var (firstPriority, firstSymbol) = acceptSymbols[0];

        for (int i = 1; i < acceptSymbols.Count; i++)
        {
            var (priority, symbol) = acceptSymbols[i];
            if (firstSymbol != symbol)
            {
                if ((options & DfaBuildOptions.PrioritizeSymbols) != 0)
                {
                    if (firstPriority > priority)
                    {
                        return firstSymbol;
                    }
                    // Conflicts between noise symbols do not cause an error because
                    // it doesn't matter which one gets chosen.
                    if (firstPriority == priority && priority == NoisePriority)
                    {
                        continue;
                    }
                }
                return default;
            }
        }

        // At this point all symbols are the same.
        return firstSymbol;
    }

    private static void WriteDfa(List<DfaState> states, DfaWriter<TChar> dfaWriter, DfaBuildOptions options)
    {
        // If there are already states in the DFA writer, adjust the numbers
        // of all the new ones.
        int stateNumberAdjustment = dfaWriter.StateCount;

        foreach (var state in states)
        {
            foreach (var (start, end, target) in state.Transitions)
            {
                if (target == state.DefaultTransition)
                {
                    continue;
                }
                dfaWriter.AddEdge(start, end, target is { } t ? t + stateNumberAdjustment : null);
            }

            if (FindDominantSymbol(state.AcceptSymbols, options) is { HasValue: true } sym)
            {
                dfaWriter.AddAccept(sym);
            }
            else
            {
                // FindDominantSymbol returning null means either:
                // 1. There are no accept symbols so we add nothing.
                // 2. There are multiple accept symbols so we add them all.
                foreach (var (_, symbol) in state.AcceptSymbols)
                {
                    dfaWriter.AddAccept(symbol);
                }
            }

            dfaWriter.FinishState(state.DefaultTransition is { } dt ? dt + stateNumberAdjustment : null);
        }
    }

    private readonly List<DfaState>? BuildDfaStates(List<RegexLeaf> leaves, List<BitSet> followPos, BitSet rootStateId, int maxStates)
    {
        Dictionary<BitSet, DfaState> states = [];
        List<DfaState> stateList = [];
        Stack<int> unmarkedStates = [];

        List<(TChar, IntervalType, int)> stateIntervals = [];
        BitArrayNeo presentLeaves = new(leaves.Count);
        List<BitSet> followPosUnionCache = [];

        bool breakOnAcceptExists = leaves.Exists(x => x is RegexLeaf.Chars { IsBreakOnAccept: true });

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
                    case RegexLeaf.End { Symbol: TokenSymbolHandle symbolIndex, Priority: int priority }:
                        S.AcceptSymbols.Add((priority, symbolIndex));
                        break;
                    case RegexLeaf.CharsBase x:
                        IntervalType startInterval = IntervalType.Start, endInterval = IntervalType.End;
                        if (x.IsInverted)
                        {
                            startInterval = x.IsHighPriorityInverted ? IntervalType.HighPriorityInvertedStart : IntervalType.InvertedStart;
                            endInterval = x.IsHighPriorityInverted ? IntervalType.HighPriorityInvertedEnd : IntervalType.InvertedEnd;
                            presentLeaves[i] = true;
                            emitDefaultTransition = true;
                            invertedCount++;
                        }
                        switch (x)
                        {
                            case RegexLeaf.Chars y:
                                foreach (var c in y.Characters)
                                {
                                    stateIntervals.Add((c, startInterval, i));
                                    stateIntervals.Add((c, endInterval, i));
                                }
                                break;
                            case RegexLeaf.CharRanges y:
                                foreach (var (start, end) in y.Ranges)
                                {
                                    stateIntervals.Add((start, startInterval, i));
                                    stateIntervals.Add((end, endInterval, i));
                                }
                                break;
                        }
                        break;
                }
            }

            stateIntervals.Sort();

            TChar? previousChar = null;
            bool previousIsStart = false;
            int depth = 0;
            int invertedDepth = 0;
            int highPriorityInvertedDepth = 0;

            foreach (var (c, type, leaf) in stateIntervals)
            {
                bool isStart = type is IntervalType.Start or IntervalType.InvertedStart or IntervalType.HighPriorityInvertedStart;
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
                        transitionRangeStart++;
                    }
                    TChar transitionRangeEnd = c;
                    if (isStart)
                    {
                        // This cannot underflow because to enter this path, a range must
                        // have already started, and only the first item in the list can
                        // have a NUL character.
                        transitionRangeEnd--;
                    }

                    // Don't emit a transition if the range start is greater than the range end.
                    // This can occur when we have three leaves with ranges [a-b], [a-a] and [b-b],
                    // causing failures later when writing the DFA.
                    if (transitionRangeStart.CompareTo(transitionRangeEnd) <= 0)
                    {
                        // We must emit an explicit failure if we are inside all the inverted leaves
                        // and only these.
                        // The presence of Any leaves will cause the above to never hold, because
                        // Any leaves are inverted Chars leaves with no ranges, which means that
                        // some inverted leaves will never be entered.
                        bool insideHighPriorityInvertedRanges = highPriorityInvertedDepth > 0;
                        bool insideAllInvertedRanges = invertedDepth == invertedCount;
                        bool insideOnlyInvertedRanges = invertedDepth == depth;
                        bool shouldEmitFailure = insideHighPriorityInvertedRanges || (insideAllInvertedRanges && insideOnlyInvertedRanges);
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
                    case IntervalType.HighPriorityInvertedStart:
                        depth++;
                        invertedDepth++;
                        highPriorityInvertedDepth++;
                        switchValue = false;
                        break;
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
                    case IntervalType.InvertedEnd:
                        depth--;
                        invertedDepth--;
                        switchValue = true;
                        break;
                    default:
                        Debug.Assert(type is IntervalType.HighPriorityInvertedEnd);
                        depth--;
                        invertedDepth--;
                        highPriorityInvertedDepth--;
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
            var followedSet = BitSet.UnionMany(followPosUnionCache);

            // We order the ANDs from least to most likely to be true.
            // It's rare for BoA leaves to exist in a build in the first place,
            // but if they do, they are expected to appear pretty much everywhere,
            // based on their current usage within Farkle.
            // TODO: This can be further optimized, although some optimizations
            // would need new APIs to BitCollections.
            if (breakOnAcceptExists
                && followedSet.Any(i => leaves[i] is RegexLeaf.End)
                && presentLeaves.Any(i => leaves[i] is RegexLeaf.Chars { IsBreakOnAccept: true }))
            {
                followPosUnionCache.Clear();
                foreach (var i in presentLeaves)
                {
                    if (leaves[i] is RegexLeaf.Chars { IsBreakOnAccept: true })
                    {
                        continue;
                    }
                    followPosUnionCache.Add(followPos[i]);
                }
                followedSet = BitSet.UnionMany(followPosUnionCache);
            }

            return followedSet;
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

    private Regex LowerRegex(Regex regex, bool caseSensitive, Dictionary<(Regex, bool CaseSensitive), Regex> loweredRegexCache)
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
                    if (caseSensitive)
                    {
                        builder.Add(Regex.Literal(c));
                    }
                    else
                    {
                        builder.Add(Regex.OneOf(ReusableRangeCanonicalizer.Canonicalize([(c, c)], false)));
                    }
                }
                result = Regex.Join(builder.MoveToImmutable());
            }
            else if (regex.IsChars(out var chars, out var flags))
            {
                var charsSpan = chars.Span;
                if (caseSensitive && RegexRangeCanonicalizer.IsCanonical(charsSpan))
                {
                    // If the regex has been canonicalized into a set of all/none characters
                    // and is/isn't inverted, change it to Regex.Void.
                    bool isInverted = (flags & Regex.CharsFlags.Inverted) != 0;
                    unsafe
                    {
                        int lengthThatTurnsToVoid = isInverted ? (1 << (sizeof(TChar) * 8)) - 1 : 0;
                        if (charsSpan.Length == lengthThatTurnsToVoid)
                        {
                            result = Regex.Void;
                        }
                        else
                        {
                            result = regex;
                        }
                    }
                }
                else
                {
                    var rangesCanonicalized = ReusableRangeCanonicalizer.Canonicalize(charsSpan, caseSensitive);
                    result = MaybeReduceToVoid(rangesCanonicalized, flags);
                }
            }
            else if (regex.IsCharRanges(out var ranges, out flags))
            {
                if (caseSensitive && RegexRangeCanonicalizer.IsCanonical(ranges.AsSpan()))
                {
                    result = regex;
                }
                else
                {
                    var rangesCanonicalized = ReusableRangeCanonicalizer.Canonicalize(ranges.AsSpan(), caseSensitive);
                    result = MaybeReduceToVoid(rangesCanonicalized, flags);
                }
            }
            else
            {
                result = regex;
            }
            loweredRegexCache[(regex, caseSensitive)] = result;
            return result;
        }

        ThrowHelpers.ThrowUnsupportedCharacterException();
        return null!;

        static Regex MaybeReduceToVoid(ImmutableArray<(char, char)> ranges, Regex.CharsFlags flags)
        {
            bool isInverted = (flags & Regex.CharsFlags.Inverted) != 0;
            // If the regex has been canonicalized into a set of all/none characters
            // and is/isn't inverted, change it to Regex.Void.
            if ((ranges, isInverted) is ([], false) or ([(char.MinValue, char.MaxValue)], true))
            {
                return Regex.Void;
            }
            return Regex.CharRanges(ranges, flags);
        }
    }

    private (List<RegexLeaf>? Leaves, List<BitSet> FollowPos, BitSet RootFirstPos) BuildRegexTree(Regex regex, DfaBuildOptions options)
    {
        Dictionary<(Regex, bool CaseSensitive), Regex> loweredRegexCache = [];
        List<RegexLeaf> leaves = [];
        List<BitSet> followPos = [];

        VisitFlags flags = VisitFlags.None;
        if ((options & DfaBuildOptions.CaseSensitive) != 0)
        {
            flags |= VisitFlags.CaseSensitive;
        }
        RegexInfo info = Visit(ref this, default, regex, flags);
        bool hasError = (info.Characteristics & RegexCharacteristics.HasError) != 0;

        return (hasError ? null : leaves, followPos, info.FirstPos);

        RegexInfo Visit(ref DfaBuild<TChar> @this, TokenSymbolHandle symbol, Regex regex, VisitFlags flags)
        {
            @this.CancellationToken.ThrowIfCancellationRequested();

            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return RegexInfo.Error(RegexCharacteristics.IsTooComplex);
            }

            flags = AdjustCaseSensitivity(regex, flags);

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
                        @this.Log.RegexStringParseError(@this.GetSymbolName(symbol), error);
                        return RegexInfo.Error();
                }
                flags = AdjustCaseSensitivity(regex, flags);
            }

            if (regex.IsAccept(out Regex? rAccepted, out var sAccepted, out var lowestPriority))
            {
                symbol = sAccepted;
                RegexInfo info = RegexInfo.Void;
                // If the symbol's regex's root is an Alt, we assign each of its children a different priority. This
                // emulates the behavior of GOLD Parser and resolves some nasty indistinguishable symbols errors.
                // Earlier versions of Farkle were flattening nested Alts. Because we are not doing that anymore,
                // this will slightly change behavior, but the impact is so small that it's not worth proactively
                // caring about.
                ReadOnlySpan<Regex> alternatives = rAccepted.IsAlt(out var altRegexes) ? altRegexes.AsSpan() : [rAccepted];
                int? endLeafIndexTerminal = null, endLeafIndexLiteral = null;
                if (lowestPriority)
                {
                    endLeafIndexTerminal = endLeafIndexLiteral = AddLeaf(new RegexLeaf.End(symbol, NoisePriority));
                }
                bool isVoid = false;
                foreach (var r in alternatives)
                {
                    var nextInfo = Visit(ref @this, symbol, r, flags);
                    int leafIndex = nextInfo.HasStar
                        ? endLeafIndexTerminal ??= AddLeaf(new RegexLeaf.End(symbol, TerminalPriority))
                        : endLeafIndexLiteral ??= AddLeaf(new RegexLeaf.End(symbol, LiteralPriority));
                    RegexInfo acceptLeaf = RegexInfo.Singleton(leafIndex).AsNullable();
                    LinkFollowPos(in nextInfo.LastPos, acceptLeaf.FirstPos);
                    isVoid &= nextInfo.LastPos.IsEmpty && !nextInfo.IsNullable;
                    info |= nextInfo + acceptLeaf;
                }
                if ((info.Characteristics & RegexCharacteristics.IsTooComplex) != 0)
                {
                    @this.Log.RegexTooComplexError(@this.GetSymbolName(symbol));
                }
                if (isVoid && @this.Log.IsEnabled(Diagnostics.DiagnosticSeverity.Warning))
                {
                    @this.Log.SymbolCannotBeMatched(@this.GetSymbolName(symbol));
                }
                return info;
            }

            if (regex.IsConcat(out ImmutableArray<Regex> regexes))
            {
                RegexInfo info = RegexInfo.Empty;
                foreach (var r in regexes)
                {
                    RegexInfo nextResult = Visit(ref @this, symbol, r, flags);
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
                    info |= Visit(ref @this, symbol, r, flags);
                }
                return info;
            }

            if (regex.IsLoop(out Regex? loopItem, out int m, out int n))
            {
                RegexInfo info = RegexInfo.Empty;
                for (int i = 0; i < m; i++)
                {
                    RegexInfo nextInfo = Visit(ref @this, symbol, loopItem, flags);
                    LinkFollowPos(in info.LastPos, in nextInfo.FirstPos);
                    info += nextInfo;
                }

                if (n == int.MaxValue)
                {
                    RegexInfo starInfo = Visit(ref @this, symbol, loopItem, flags).AsStar();
                    LinkFollowPos(in starInfo.LastPos, in starInfo.FirstPos);
                    LinkFollowPos(in info.LastPos, in starInfo.FirstPos);
                    info += starInfo;
                }
                else
                {
                    for (int i = m; i < n; i++)
                    {
                        RegexInfo nextInfo = Visit(ref @this, symbol, loopItem, flags).AsNullable();
                        LinkFollowPos(in info.LastPos, in nextInfo.FirstPos);
                        info += nextInfo;
                    }
                }
                return info;
            }

            if ((flags & VisitFlags.Lowered) == 0)
            {
                regex = @this.LowerRegex(regex, (flags & VisitFlags.CaseSensitive) != 0, loweredRegexCache);
            }

            if (regex.IsAny())
            {
                return RegexInfo.Singleton(AddLeaf(RegexLeaf.Any));
            }

            if (IsRegexChars(regex, out var chars, out var charsFlags))
            {
                return RegexInfo.Singleton(AddLeaf(new RegexLeaf.Chars(chars, charsFlags)));
            }

            if (IsRegexCharRanges(regex, out var ranges, out charsFlags))
            {
                return RegexInfo.Singleton(AddLeaf(new RegexLeaf.CharRanges(ranges, charsFlags)));
            }

            if ((flags & VisitFlags.Lowered) == 0)
            {
                return Visit(ref @this, symbol, regex, flags | VisitFlags.Lowered);
            }

            throw new InvalidOperationException("Internal error: unrecognized form of lowered regex.");

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

            static VisitFlags AdjustCaseSensitivity(Regex regex, VisitFlags flags)
            {
                if ((flags & VisitFlags.CaseOverridden) == 0 && regex.TryGetCaseSensitivity(out bool isCaseSensitive))
                {
                    flags |= VisitFlags.CaseOverridden;
                    if (isCaseSensitive)
                    {
                        return flags | VisitFlags.CaseSensitive;
                    }
                    else
                    {
                        return flags & ~VisitFlags.CaseSensitive;
                    }
                }
                return flags;
            }
        }
    }

    private sealed class DfaState(BitSet stateId, int index)
    {
        public BitSet StateId { get; } = stateId;

        public int Index { get; } = index;

        public List<(TChar, TChar, int?)> Transitions { get; } = [];

        public int? DefaultTransition { get; set; }

        public List<(int Priority, TokenSymbolHandle Symbol)> AcceptSymbols { get; } = [];

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
                    if (start.CompareTo(TChar.MinValue) > 0)
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
                if (lastEnd.CompareTo(start - TChar.One) != 0)
                {
                    return false;
                }
                lastEnd = end;
            }

            return lastEnd.CompareTo(TChar.MaxValue) == 0;
        }
    }

    private abstract class RegexLeaf
    {
        public static Chars Any { get; } = new Chars([], Regex.CharsFlags.Inverted);

        public abstract class CharsBase(Regex.CharsFlags flags) : RegexLeaf
        {
            public bool IsInverted => (flags & Regex.CharsFlags.Inverted) != 0;

            public bool IsHighPriorityInverted =>
                (flags & Regex.CharsFlags.HighPriorityInverted) == Regex.CharsFlags.HighPriorityInverted;

            public bool IsBreakOnAccept => (flags & Regex.CharsFlags.BreakOnAccept) != 0;
        }

        public sealed class Chars(ImmutableBuffer<TChar> chars, Regex.CharsFlags flags) : CharsBase(flags)
        {
            public ReadOnlySpan<TChar> Characters => chars.Span;
        }

        public sealed class CharRanges(ImmutableArray<(TChar Start, TChar End)> ranges, Regex.CharsFlags flags) : CharsBase(flags)
        {
            public ImmutableArray<(TChar Start, TChar End)> Ranges { get; } = ranges;
        }

        public sealed class End(TokenSymbolHandle symbol, int priority) : RegexLeaf
        {
            public TokenSymbolHandle Symbol { get; } = symbol;

            public int Priority { get; } = priority;
        }
    }

    private readonly struct RegexInfo(BitSet FirstPos, BitSet LastPos, bool IsNullable,
        RegexCharacteristics Characteristics = RegexCharacteristics.None)
    {
        public readonly BitSet FirstPos = FirstPos;

        public readonly BitSet LastPos = LastPos;

        public bool IsNullable { get; } = IsNullable;

        public RegexCharacteristics Characteristics { get; } = Characteristics;

        public bool HasStar => (Characteristics & RegexCharacteristics.HasStar) != 0;

        public RegexInfo AsNullable() =>
            new(FirstPos, LastPos, true, Characteristics);

        public RegexInfo AsStar() =>
            new(FirstPos, LastPos, true, Characteristics | RegexCharacteristics.HasStar);

        public static RegexInfo Empty => new(BitSet.Empty, BitSet.Empty, true);

        public static RegexInfo Void => new(BitSet.Empty, BitSet.Empty, false);

        public static RegexInfo Error(RegexCharacteristics extraCharacteristics = 0) =>
            new(BitSet.Empty, BitSet.Empty, false, RegexCharacteristics.HasError | extraCharacteristics);

        public static RegexInfo Singleton(int index)
        {
            BitSet pos = BitSet.Singleton(index);
            return new RegexInfo(pos, pos, IsNullable: false);
        }

        public static RegexInfo operator +(in RegexInfo left, in RegexInfo right)
        {
            return new RegexInfo(
                left.IsNullable ? BitSet.Union(in left.FirstPos, in right.FirstPos) : left.FirstPos,
                right.IsNullable ? BitSet.Union(in left.LastPos, in right.LastPos) : right.LastPos,
                left.IsNullable && right.IsNullable,
                left.Characteristics | right.Characteristics);
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
        /// Processing of the regex failed for some reason. The builder will continue
        /// processing the regexes to uncover more errors, but will not emit a DFA.
        /// </summary>
        HasError = 2,
        /// <summary>
        /// Processing of the regex failed because it is too complex.
        /// Must be specified alongside <see cref="HasError"/>.
        /// </summary>
        /// <remarks>
        /// This flag exists to report an error only once per symbol.
        /// </remarks>
        IsTooComplex = 4,
    }

    [Flags]
    private enum VisitFlags : byte
    {
        None = 0,
        CaseSensitive = 1,
        CaseOverridden = 2,
        Lowered = 4,
    }

    private enum IntervalType : byte
    {
        // It is important that the Start values are before the End values.
        HighPriorityInvertedStart,
        Start,
        InvertedStart,
        InvertedEnd,
        End,
        HighPriorityInvertedEnd,
    }
}
