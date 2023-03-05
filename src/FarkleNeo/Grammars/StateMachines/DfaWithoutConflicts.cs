// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithoutConflicts<TChar, TState, TEdge, TTokenSymbol> : DfaImplementationBase<TChar, TState, TEdge> where TChar : unmanaged, IComparable<TChar>
{
    internal required int AcceptBase { get; init; }

    public DfaWithoutConflicts(Grammar grammar, int stateCount, int edgeCount) : base(grammar, stateCount, edgeCount, false) { }

    public static DfaWithoutConflicts<TChar, TState, TEdge, TTokenSymbol> Create(Grammar grammar, int stateCount, int edgeCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
    {
        int expectedSize =
            sizeof(uint) * 2
            + stateCount * sizeof(TEdge)
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * sizeof(TState)
            + stateCount * sizeof(TTokenSymbol);

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 2;
        int rangeFromBase = firstEdgeBase + stateCount * sizeof(TEdge);
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int acceptBase = edgeTargetBase + edgeCount * sizeof(TState);

        if (dfaDefaultTransitions.Length > 0)
        {
            if (dfaDefaultTransitions.Length != stateCount * sizeof(TState))
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

        if (GetSingleAcceptSymbol(state).HasValue)
        {
            return (state, 1);
        }

        return (0, 0);
    }

    internal override TokenSymbolHandle GetAcceptSymbolAt(int index) => GetSingleAcceptSymbol(index);

    internal override TokenSymbolHandle GetSingleAcceptSymbol(int state)
    {
        ValidateStateIndex(state);
        return new(_grammar.GrammarFile.ReadUIntVariableSize<TTokenSymbol>(AcceptBase + state * sizeof(TTokenSymbol)));
    }

    internal override bool StateHasConflicts(int state) => false;
}
