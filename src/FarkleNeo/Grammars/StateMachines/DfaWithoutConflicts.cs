// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithoutConflicts<TChar, TIndex> : DfaImplementationBase<TChar, TIndex> where TChar : unmanaged, IComparable<TChar>
{
    internal required int AcceptBase { get; init; }

    public DfaWithoutConflicts(Grammar grammar, int stateCount, int edgeCount) : base(grammar, stateCount, edgeCount, false) { }

    public static DfaWithoutConflicts<TChar, TIndex> Create(Grammar grammar, int stateCount, int edgeCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
    {
        int expectedSize =
            sizeof(uint) * 2
            + stateCount * sizeof(TIndex)
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex);

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 2;
        int rangeFromBase = firstEdgeBase + stateCount * sizeof(TIndex);
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int acceptBase = edgeTargetBase + edgeCount * sizeof(TIndex);

        if (dfaDefaultTransitions.Length > 0)
        {
            if (dfaDefaultTransitions.Length != stateCount * sizeof(TIndex))
            {
                ThrowHelpers.ThrowInvalidDfaDataSize();
            }
        }

        return new(grammar, stateCount, edgeCount)
        {
            FirstEdgeBase = firstEdgeBase,
            RangeFromBase = rangeFromBase,
            RangeToBase = rangeToBase,
            EdgeTargetBase = edgeTargetBase,
            DefaultTransitionBase = dfaDefaultTransitions.Offset,
            AcceptBase = acceptBase
        };
    }

    internal override (int Offset, int Count) GetAcceptSymbolBounds(int state)
    {
        ValidateStateIndex(state);

        if (GetAcceptSymbol(state).HasValue)
        {
            return (state, 1);
        }

        return (0, 0);
    }

    internal override TokenSymbolHandle GetAcceptSymbolAt(int index) => GetAcceptSymbol(index);

    private TokenSymbolHandle GetAcceptSymbolUnsafe(ReadOnlySpan<byte> grammarFile, int state) =>
        new(grammarFile.ReadUIntVariableSize<TIndex>(AcceptBase + state * sizeof(TIndex)));

    public override TokenSymbolHandle GetAcceptSymbol(int state)
    {
        ValidateStateIndex(state);
        return GetAcceptSymbolUnsafe(Grammar.GrammarFile, state);
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        base.ValidateContent(grammarFile, grammarTables);

        for (int state = 0; state < Count; state++)
        {
            TokenSymbolHandle acceptSymbol = GetAcceptSymbolUnsafe(grammarFile, state);
            if (acceptSymbol.HasValue)
            {
                grammarTables.ValidateHandle(acceptSymbol);
            }
        }
    }

    internal override bool StateHasConflicts(int state) => false;
}
