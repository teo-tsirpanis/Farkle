// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe abstract class DfaImplementationBase<TChar> : Dfa<TChar> where TChar : unmanaged, IComparable<TChar>
{
    protected readonly byte _stateIndexSize, _edgeIndexSize, _tokenSymbolIndexSize;

    protected readonly int _edgeCount;

    public required int FirstEdgeBase { get; init; }

    public required int RangeFromBase { get; init; }

    public required int RangeToBase { get; init; }

    public required int EdgeTargetBase { get; init; }

    public required int DefaultTransitionBase { get; init; }

    public required int AcceptBase { get; init; }

    protected DfaImplementationBase(Grammar grammar, int stateCount, int edgeCount, int tokenSymbolCount, bool hasConflicts) : base(stateCount, hasConflicts)
    {
        _stateIndexSize = GrammarUtilities.GetCompressedIndexSize(stateCount);
        _edgeIndexSize = GrammarUtilities.GetCompressedIndexSize(edgeCount);
        _tokenSymbolIndexSize = GrammarUtilities.GetCompressedIndexSize(tokenSymbolCount);

        Grammar = grammar;
        _edgeCount = edgeCount;
    }

    protected int ReadFirstEdge(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize(FirstEdgeBase + state * _edgeIndexSize, _edgeIndexSize);

    protected int ReadState(ReadOnlySpan<byte> grammarFile, int @base, int index) =>
        (int)grammarFile.ReadUIntVariableSize(@base + index * _stateIndexSize, _stateIndexSize) - 1;

    protected TokenSymbolHandle ReadAcceptSymbol(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(AcceptBase + index * _tokenSymbolIndexSize, _tokenSymbolIndexSize));

    internal sealed override Grammar Grammar { get; }

    private int GetDefaultTransitionUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        return ReadState(grammarFile, DefaultTransitionBase, state);
    }

    private (int Offset, int Count) GetEdgeBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int nextEdgeOffset = state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount;
        return (edgeOffset, nextEdgeOffset - edgeOffset);
    }

    private DfaEdge<TChar> GetEdgeAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        TChar cFrom = StateMachineUtilities.Read<TChar>(grammarFile, RangeFromBase + index * sizeof(char));
        TChar cTo = StateMachineUtilities.Read<TChar>(grammarFile, RangeToBase + index * sizeof(char));
        int target = ReadState(grammarFile, EdgeTargetBase, + index);

        return new(cFrom, cTo, target);
    }

    internal sealed override int GetDefaultTransition(int state)
    {
        ValidateStateIndex(state);
        if (DefaultTransitionBase == 0)
        {
            return -1;
        }
        return GetDefaultTransitionUnsafe(Grammar.GrammarFile, state);
    }

    internal sealed override (int Offset, int Count) GetEdgeBounds(int state)
    {
        ValidateStateIndex(state);
        return GetEdgeBoundsUnsafe(Grammar.GrammarFile, state);
    }

    internal sealed override DfaEdge<TChar> GetEdgeAt(int index)
    {
        if ((uint)index >= (uint)_edgeCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return GetEdgeAtUnsafe(Grammar.GrammarFile, index);
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        {
            int previousFirstEdge = ReadFirstEdge(grammarFile, 0);
            Assert(previousFirstEdge == 0);
            for (int i = 1; i < Count; i++)
            {
                int firstAction = ReadFirstEdge(grammarFile, i);
                Assert(firstAction >= previousFirstEdge, "DFA state first edge is out of sequence.");
                previousFirstEdge = firstAction;
                Assert(firstAction <= _edgeCount);
            }
        }

        for (int i = 0; i < Count; i++)
        {
            (int edgeOffset, int edgeCount) = GetEdgeBoundsUnsafe(grammarFile, i);
            if (edgeCount > 0)
            {
                TChar previousKeyTo = GetEdgeAtUnsafe(grammarFile, edgeOffset).KeyTo;
                for (int j = 0; j < edgeCount; j++)
                {
                    DfaEdge<TChar> edge = GetEdgeAtUnsafe(grammarFile, edgeOffset + j);
                    Assert(edge.KeyFrom.CompareTo(edge.KeyTo) <= 0, "DFA state edge range is inverted.");
                    if (j != 0)
                    {
                        Assert(previousKeyTo.CompareTo(edge.KeyFrom) < 0, "DFA state edges are unsorted.");
                    }
                    previousKeyTo = edge.KeyTo;
                    ValidateEdgeTarget(edge.Target);
                }
            }
        }

        if (DefaultTransitionBase != 0)
        {
            for (int i = 0; i < Count; i++)
            {
                int defaultTransition = GetDefaultTransitionUnsafe(grammarFile, i);
                ValidateEdgeTarget(defaultTransition);
            }
        }

        static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                ThrowHelpers.ThrowInvalidDataException(message);
            }
        }
    }

    private void ValidateEdgeTarget(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if (state != -1)
        {
            ValidateStateIndex(state, paramName);
        }
    }

    protected void ValidateStateIndex(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= (uint)Count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }
}
