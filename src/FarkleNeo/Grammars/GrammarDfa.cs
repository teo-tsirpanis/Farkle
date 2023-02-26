// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars;

internal unsafe sealed class GrammarDfa<TChar, TState, TEdge, TTokenSymbol> : Dfa<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private readonly Grammar _grammar;

    private readonly bool _hasDefaultTransitions;

    private readonly int _edgeCount, _firstEdgeBase, _rangeFromBase, _rangeToBase, _edgeTargetBase, _acceptBase, _defaultTransitionBase;

    public override int Count { get; }

    public override bool HasConflicts => false;

    public GrammarDfa(Grammar grammar, int stateCount, int edgeCount, int dfaOffset, int dfaLength, int dfaDefaultTransitionsOffset, int dfaDefaultTransitionsLength)
    {
        _grammar = grammar;

        Count = stateCount;
        _edgeCount = edgeCount;
        Debug.Assert(GrammarTables.GetIndexSize(Count) == sizeof(TState));
        Debug.Assert(GrammarTables.GetIndexSize(Count) == sizeof(TEdge));

        int expectedSize =
            sizeof(uint) * 2
            + Count * sizeof(TEdge)
            + _edgeCount * sizeof(TChar) * 2
            + _edgeCount * sizeof(TState)
            + Count * sizeof(TTokenSymbol);

        if (dfaLength != expectedSize)
        {
            ThrowInvalidDfaDataSize();
        }

        _firstEdgeBase = dfaOffset + sizeof(uint) * 2;
        _rangeFromBase = _firstEdgeBase + Count * sizeof(TEdge);
        _rangeToBase = _rangeFromBase + _edgeCount * sizeof(TChar);
        _edgeTargetBase = _rangeToBase + _edgeCount * sizeof(TChar);
        _acceptBase = _edgeTargetBase + _edgeCount * sizeof(TState);

        if (dfaDefaultTransitionsLength > 0)
        {
            if (dfaDefaultTransitionsLength != Count * sizeof(TState))
            {
                ThrowInvalidDfaDataSize();
            }

            _hasDefaultTransitions = true;
            _defaultTransitionBase = dfaDefaultTransitionsOffset;
        }
    }

    private int ReadFirstEdge(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize<TEdge>(_firstEdgeBase + state * sizeof(TEdge));

    private static int ReadState(ReadOnlySpan<byte> grammarFile, int @base) =>
        (int)grammarFile.ReadUIntVariableSize<TState>(@base) - 1;

    public override int NextState(int state, TChar c)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int edgeLength = (state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount) - edgeOffset;

        if (edgeLength != 0)
        {
            int edge = StateMachineUtilities.BufferBinarySearch(grammarFile, _rangeToBase + edgeOffset * sizeof(TChar), edgeLength, c);

            if (edge < 0)
            {
                edge = Math.Min(~edge, edgeLength - 1);
            }

            TChar cFrom = StateMachineUtilities.ReadChar<TChar>(grammarFile, _rangeFromBase + (edgeOffset + edge) * sizeof(char));
            TChar cTo = StateMachineUtilities.ReadChar<TChar>(grammarFile, _rangeToBase + (edgeOffset + edge) * sizeof(char));

            if (cFrom.CompareTo(c) <= 0 && c.CompareTo(cTo) <= 0)
            {
                return ReadState(grammarFile, _edgeTargetBase + (edgeOffset + edge) * sizeof(TState));
            }
        }

        if (_hasDefaultTransitions)
        {
            return ReadState(grammarFile, _defaultTransitionBase + state * sizeof(TState));
        }

        return -1;
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

    internal override TokenSymbolHandle GetAcceptSymbol(int index) => GetSingleAcceptSymbol(index);

    internal override int GetDefaultTransition(int state)
    {
        ValidateStateIndex(state);

        if (_defaultTransitionBase == 0)
        {
            return -1;
        }

        return ReadState(_grammar.GrammarFile, _defaultTransitionBase + state * sizeof(TState));
    }

    internal override (int Offset, int Count) GetEdgeBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int nextEdgeOffset = state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount;

        return (edgeOffset, nextEdgeOffset - edgeOffset);
    }

    internal override DfaEdge<TChar> GetEdge(int index)
    {
        if((uint)index >= (uint)_edgeCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        TChar cFrom = StateMachineUtilities.ReadChar<TChar>(grammarFile, _rangeFromBase + index * sizeof(char));
        TChar cTo = StateMachineUtilities.ReadChar<TChar>(grammarFile, _rangeToBase + index * sizeof(char));
        int target = ReadState(grammarFile, _edgeTargetBase + index * sizeof(TState));

        return new(cFrom, cTo, target);
    }

    internal override TokenSymbolHandle GetSingleAcceptSymbol(int state)
    {
        ValidateStateIndex(state);
        return new(_grammar.GrammarFile.ReadUIntVariableSize<TTokenSymbol>(_acceptBase + state * sizeof(TTokenSymbol)));
    }

    internal override bool StateHasConflicts(int state) => false;

    private void ValidateStateIndex(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= (uint)Count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }

    [DoesNotReturn, StackTraceHidden]
    private static void ThrowInvalidDfaDataSize() =>
        ThrowHelpers.ThrowInvalidDataException("Invalid DFA data size.");
}
