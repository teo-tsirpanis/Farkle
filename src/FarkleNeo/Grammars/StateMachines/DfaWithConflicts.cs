// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithConflicts<TChar, TState, TEdge, TTokenSymbol, TAccept> : DfaImplementationBase<TChar, TState, TEdge> where TChar : unmanaged, IComparable<TChar>
{
    private readonly int _acceptCount;

    internal required int FirstAcceptBase { get; init; }

    internal required int AcceptBase { get; init; }

    public DfaWithConflicts(Grammar grammar, int stateCount, int edgeCount, int acceptCount) : base(grammar, stateCount, edgeCount, true)
    {
        _acceptCount = acceptCount;
    }

    public static DfaWithConflicts<TChar, TState, TEdge, TTokenSymbol, TAccept> Create(Grammar grammar, int stateCount, int edgeCount, int acceptCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
    {
        int expectedSize =
            sizeof(uint) * 3
            + stateCount * sizeof(TEdge)
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * sizeof(TState)
            + stateCount * sizeof(TAccept)
            + acceptCount * sizeof(TTokenSymbol);

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 3;
        int rangeFromBase = firstEdgeBase + stateCount * sizeof(TEdge);
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int firstAcceptBase = edgeTargetBase + edgeCount * sizeof(TState);
        int acceptBase = firstAcceptBase + stateCount * sizeof(TAccept);

        if (dfaDefaultTransitions.Length > 0)
        {
            if (dfaDefaultTransitions.Length != stateCount * sizeof(TState))
            {
                ThrowHelpers.ThrowInvalidDfaDataSize();
            }
        }

        return new(grammar, stateCount, edgeCount, acceptCount)
        {
            FirstEdgeBase = firstEdgeBase,
            RangeFromBase = rangeFromBase,
            RangeToBase = rangeToBase,
            EdgeTargetBase = edgeTargetBase,
            DefaultTransitionBase = dfaDefaultTransitions.Offset,
            FirstAcceptBase = firstAcceptBase,
            AcceptBase = acceptBase
        };
    }

    private int ReadFirstAccept(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize<TAccept>(FirstAcceptBase + state * sizeof(TAccept));

    internal override (int Offset, int Count) GetAcceptSymbolBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;
        int firstAccept = ReadFirstAccept(grammarFile, state);
        int nextFirstAccept = state != Count - 1 ? ReadFirstAccept(grammarFile, state + 1) : _acceptCount;
        return (firstAccept,  nextFirstAccept - firstAccept);
    }

    internal override TokenSymbolHandle GetAcceptSymbolAt(int index)
    {
        if ((uint)index >= (uint)_acceptCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return new(Grammar.GrammarFile.ReadUIntVariableSize<TTokenSymbol>(AcceptBase + index * sizeof(TTokenSymbol)));
    }

    public override TokenSymbolHandle GetAcceptSymbol(int state) =>
        throw new NotSupportedException("This method is not supported for DFAs with conflicts.");
}
