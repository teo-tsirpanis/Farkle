// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithConflicts<TChar> : DfaImplementationBase<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private readonly byte _acceptIndexSize;

    private readonly int _acceptCount;

    internal required int FirstAcceptBase { get; init; }

    [SetsRequiredMembers]
    public DfaWithConflicts(Grammar grammar, int stateCount, int edgeCount, int acceptCount, int tokenSymbolCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
        : base(grammar, stateCount, edgeCount, tokenSymbolCount, true)
    {
        _acceptIndexSize = GrammarUtilities.GetCompressedIndexSize(acceptCount);
        _acceptCount = acceptCount;

        int expectedSize =
            sizeof(uint) * 3
            + stateCount * _edgeIndexSize
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * _stateIndexSize
            + stateCount * _acceptIndexSize
            + acceptCount * _tokenSymbolIndexSize;

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 3;
        int rangeFromBase = firstEdgeBase + stateCount * _edgeIndexSize;
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int firstAcceptBase = edgeTargetBase + edgeCount * _stateIndexSize;
        int acceptBase = firstAcceptBase + stateCount * _acceptIndexSize;

        if (dfaDefaultTransitions.Length > 0)
        {
            if (dfaDefaultTransitions.Length != stateCount * _stateIndexSize)
            {
                ThrowHelpers.ThrowInvalidDfaDataSize();
            }
        }

        FirstEdgeBase = firstEdgeBase;
        RangeFromBase = rangeFromBase;
        RangeToBase = rangeToBase;
        EdgeTargetBase = edgeTargetBase;
        DefaultTransitionBase = dfaDefaultTransitions.Offset;
        AcceptBase = acceptBase;
        FirstAcceptBase = firstAcceptBase;
    }

    private int ReadFirstAccept(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize(FirstAcceptBase + state * _acceptIndexSize, _acceptIndexSize);

    private (int Offset, int Count) GetAcceptSymbolBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int firstAccept = ReadFirstAccept(grammarFile, state);
        int nextFirstAccept = state != Count - 1 ? ReadFirstAccept(grammarFile, state + 1) : _acceptCount;
        return (firstAccept, nextFirstAccept - firstAccept);
    }

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
        return ReadAcceptSymbol(Grammar.GrammarFile, index);
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
            TokenSymbolHandle acceptSymbol = ReadAcceptSymbol(grammarFile, i);
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
