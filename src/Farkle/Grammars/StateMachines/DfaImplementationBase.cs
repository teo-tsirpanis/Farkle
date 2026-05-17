// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal abstract class DfaImplementationBase<TChar> : Dfa<TChar> where TChar : unmanaged, IComparable<TChar>
{
    protected readonly byte _stateIndexSize, _edgeIndexSize, _tokenSymbolIndexSize;

    protected readonly int _edgeCount;

    private readonly int _groupCount;

    public required int FirstEdgeBase { get; init; }

    public required int RangeFromBase { get; init; }

    public required int RangeToBase { get; init; }

    public required int EdgeTargetBase { get; init; }

    public required int AcceptBase { get; init; }

    public int DefaultTransitionBase { get; }

    public int GroupStartStateBase { get; }

    protected DfaImplementationBase(Grammar grammar, int stateCount, int edgeCount, in GrammarStateMachines.Dfa dfa, bool hasConflicts) : base(grammar, stateCount, hasConflicts)
    {
        _stateIndexSize = GrammarUtilities.GetCompressedIndexSize(stateCount);
        _edgeIndexSize = GrammarUtilities.GetCompressedIndexSize(edgeCount);
        _tokenSymbolIndexSize = GrammarUtilities.GetCompressedIndexSize(grammar.GrammarTables.TokenSymbolRowCount);

        _edgeCount = edgeCount;
        _groupCount = grammar.GrammarTables.GroupRowCount;

        if (dfa.DefaultTransitions.Length > 0 && dfa.DefaultTransitions.Length != stateCount * _stateIndexSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        if (dfa.GroupStartStates.Length > 0 && dfa.GroupStartStates.Length != _groupCount * _stateIndexSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        DefaultTransitionBase = dfa.DefaultTransitions.Offset;
        GroupStartStateBase = dfa.GroupStartStates.Offset;
    }

    protected int ReadFirstEdge(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize(FirstEdgeBase + state * _edgeIndexSize, _edgeIndexSize);

    protected int ReadState(ReadOnlySpan<byte> grammarFile, int @base, int index) =>
        (int)grammarFile.ReadUIntVariableSize(@base + index * _stateIndexSize, _stateIndexSize) - 1;

    protected TokenSymbolHandle ReadAcceptSymbol(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(AcceptBase + index * _tokenSymbolIndexSize, _tokenSymbolIndexSize));

    protected int GetDefaultTransitionUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        if (DefaultTransitionBase == 0)
        {
            return -1;
        }
        return ReadState(grammarFile, DefaultTransitionBase, state);
    }

    protected (int Offset, int Count) GetEdgeBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int nextEdgeOffset = state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount;
        return (edgeOffset, nextEdgeOffset - edgeOffset);
    }

    private DfaEdge<TChar> GetEdgeAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        TChar cFrom = StateMachineUtilities.Read<TChar>(grammarFile, RangeFromBase + index * sizeof(char));
        TChar cTo = StateMachineUtilities.Read<TChar>(grammarFile, RangeToBase + index * sizeof(char));
        int target = ReadState(grammarFile, EdgeTargetBase, index);

        return new(cFrom, cTo, target);
    }

    private int GetGroupStartStateUnsafe(ReadOnlySpan<byte> grammarFile, uint groupIndex)
    {
        return ReadState(grammarFile, GroupStartStateBase, (int)groupIndex - 1);
    }

    internal sealed override int GetDefaultTransition(int state)
    {
        ValidateStateIndex(state);
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

    internal sealed override int GetStartStateForGroupImpl(GroupHandle group)
    {
        if (group.TableIndex > (uint)_groupCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(group));
        }
        if (GroupStartStateBase == 0)
        {
            return StartState;
        }
        return GetGroupStartStateUnsafe(Grammar.GrammarFile, group.TableIndex);
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

        if (GroupStartStateBase != 0)
        {
            for (uint i = 1; i <= _groupCount; i++)
            {
                int groupStartState = GetGroupStartStateUnsafe(grammarFile, i);
                ValidateStateIndex(groupStartState);
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
