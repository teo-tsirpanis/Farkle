// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithConflicts<TChar, TState, TEdge, TTokenSymbol, TAccept> : DfaImplementationBase<TChar, TState, TEdge> where TChar : unmanaged, IComparable<TChar>
{
    private readonly int _acceptCount;

    internal required int FirstAcceptBase { get; init; }

    internal required int AcceptBase { get; init; }

    public DfaWithConflicts(Grammar grammar, int stateCount, int edgeCount, int acceptCount) : base(grammar, stateCount, edgeCount, true)
    {
        Debug.Assert(GrammarUtilities.GetCompressedIndexSize(acceptCount) == sizeof(TAccept));

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

    private (int Offset, int Count) GetAcceptSymbolBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int firstAccept = ReadFirstAccept(grammarFile, state);
        int nextFirstAccept = state != Count - 1 ? ReadFirstAccept(grammarFile, state + 1) : _acceptCount;
        return (firstAccept, nextFirstAccept - firstAccept);
    }

    private TokenSymbolHandle GetAcceptSymbolAtUnsafe(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize<TTokenSymbol>(AcceptBase + index * sizeof(TTokenSymbol)));

    internal override (int Offset, int Count) GetAcceptSymbolBounds(int state)
    {
        ValidateStateIndex(state);
        return GetAcceptSymbolBoundsUnsafe(Grammar.GrammarFile, state);
    }

    internal override TokenSymbolHandle GetAcceptSymbolAt(int index)
    {
        if ((uint)index >= (uint)_acceptCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return GetAcceptSymbolAtUnsafe(Grammar.GrammarFile, index);
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        base.ValidateContent(grammarFile, grammarTables);

        if (_acceptCount != 0)
        {
            int previousFirstAccept = ReadFirstAccept(grammarFile, 0);
            Assert(previousFirstAccept == 0);
            for (int i = 1; i < Count; i++)
            {
                int firstAccept = ReadFirstAccept(grammarFile, i);
                Assert(firstAccept > previousFirstAccept);
                previousFirstAccept = firstAccept;
                Assert(firstAccept <= _acceptCount);
            }
        }

        for (int i = 0; i < Count; i++)
        {
            TokenSymbolHandle acceptSymbol = GetAcceptSymbolAtUnsafe(grammarFile, i);
            grammarTables.ValidateHandle(acceptSymbol);
        }

        static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                ThrowHelpers.ThrowInvalidDataException(message);
            }
        }
    }

    public override TokenSymbolHandle GetAcceptSymbol(int state) =>
        throw new NotSupportedException("This method is not supported for DFAs with conflicts.");
}
