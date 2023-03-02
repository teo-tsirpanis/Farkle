// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe abstract class DfaImplementationBase<TChar, TState, TEdge> : Dfa<TChar> where TChar : unmanaged, IComparable<TChar>
{
    protected readonly Grammar _grammar;

    private readonly int _edgeCount;

    public required int FirstEdgeBase { get; init; }

    public required int RangeFromBase { get; init; }

    public required int RangeToBase { get; init; }

    public required int EdgeTargetBase { get; init; }

    public required int DefaultTransitionBase { get; init; }

    public sealed override int Count { get; }

    protected DfaImplementationBase(Grammar grammar, int stateCount, int edgeCount)
    {
        Debug.Assert(GrammarTables.GetIndexSize(stateCount) == sizeof(TState));
        Debug.Assert(GrammarTables.GetIndexSize(edgeCount) == sizeof(TEdge));

        _grammar = grammar;
        Count = stateCount;
        _edgeCount = edgeCount;
    }

    private int ReadFirstEdge(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize<TEdge>(FirstEdgeBase + state * sizeof(TEdge));

    private static int ReadState(ReadOnlySpan<byte> grammarFile, int @base) =>
        (int)grammarFile.ReadUIntVariableSize<TState>(@base) - 1;

    public sealed override int NextState(int state, TChar c)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int edgeLength = (state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount) - edgeOffset;

        if (edgeLength != 0)
        {
            int edge = StateMachineUtilities.BufferBinarySearch(grammarFile, RangeToBase + edgeOffset * sizeof(TChar), edgeLength, c);

            if (edge < 0)
            {
                edge = Math.Min(~edge, edgeLength - 1);
            }

            TChar cFrom = StateMachineUtilities.ReadChar<TChar>(grammarFile, RangeFromBase + (edgeOffset + edge) * sizeof(char));
            TChar cTo = StateMachineUtilities.ReadChar<TChar>(grammarFile, RangeToBase + (edgeOffset + edge) * sizeof(char));

            if (cFrom.CompareTo(c) <= 0 && c.CompareTo(cTo) <= 0)
            {
                return ReadState(grammarFile, EdgeTargetBase + (edgeOffset + edge) * sizeof(TState));
            }
        }

        if (DefaultTransitionBase != 0)
        {
            return ReadState(grammarFile, DefaultTransitionBase + state * sizeof(TState));
        }

        return -1;
    }

    internal sealed override int GetDefaultTransition(int state)
    {
        ValidateStateIndex(state);

        if (DefaultTransitionBase == 0)
        {
            return -1;
        }

        return ReadState(_grammar.GrammarFile, DefaultTransitionBase + state * sizeof(TState));
    }

    internal sealed override (int Offset, int Count) GetEdgeBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int nextEdgeOffset = state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount;

        return (edgeOffset, nextEdgeOffset - edgeOffset);
    }

    internal sealed override DfaEdge<TChar> GetEdge(int index)
    {
        if ((uint)index >= (uint)_edgeCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }

        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;

        TChar cFrom = StateMachineUtilities.ReadChar<TChar>(grammarFile, RangeFromBase + index * sizeof(char));
        TChar cTo = StateMachineUtilities.ReadChar<TChar>(grammarFile, RangeToBase + index * sizeof(char));
        int target = ReadState(grammarFile, EdgeTargetBase + index * sizeof(TState));

        return new(cFrom, cTo, target);
    }

    protected void ValidateStateIndex(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= (uint)Count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }
}
