// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithConflicts<TChar, TIndex> : DfaImplementationBase<TChar, TIndex> where TChar : unmanaged, IComparable<TChar>
{
    private readonly int _acceptCount;

    internal required int FirstAcceptBase { get; init; }

    internal required int AcceptBase { get; init; }

    public DfaWithConflicts(Grammar grammar, int stateCount, int edgeCount, int acceptCount) : base(grammar, stateCount, edgeCount, true)
    {
        _acceptCount = acceptCount;
    }

    public static DfaWithConflicts<TChar, TIndex> Create(Grammar grammar, int stateCount, int edgeCount, int acceptCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
    {
        int expectedSize =
            sizeof(uint) * 3
            + stateCount * sizeof(TIndex)
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex)
            + acceptCount * sizeof(TIndex);

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 3;
        int rangeFromBase = firstEdgeBase + stateCount * sizeof(TIndex);
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int firstAcceptBase = edgeTargetBase + edgeCount * sizeof(TIndex);
        int acceptBase = firstAcceptBase + stateCount * sizeof(TIndex);

        if (dfaDefaultTransitions.Length > 0 && dfaDefaultTransitions.Length != stateCount * sizeof(TIndex))
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
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
        (int)grammarFile.ReadUIntVariableSize<TIndex>(FirstAcceptBase + state * sizeof(TIndex));

    private (int Offset, int Count) GetAcceptSymbolBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int firstAccept = ReadFirstAccept(grammarFile, state);
        int nextFirstAccept = state != Count - 1 ? ReadFirstAccept(grammarFile, state + 1) : _acceptCount;
        return (firstAccept, nextFirstAccept - firstAccept);
    }

    private TokenSymbolHandle GetAcceptSymbolAtUnsafe(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize<TIndex>(AcceptBase + index * sizeof(TIndex)));

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
                Assert(firstAccept >= previousFirstAccept);
                previousFirstAccept = firstAccept;
                Assert(firstAccept <= _acceptCount);
            }
        }

        for (int i = 0; i < _acceptCount; i++)
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
}
