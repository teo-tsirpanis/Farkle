// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Farkle.Grammars.GrammarUtilities;

namespace Farkle.Grammars.Writers;

internal class DfaWriter<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private int _currentState;

    private readonly int[] _firstEdges;
    // We use a tuple instead of DfaEdge to avoid writing our own comparer.
    private readonly List<(TChar KeyFrom, TChar KeyTo, int TargetState)> _edges = new();
    private readonly int[] _defaultTransitions;

    private readonly int[] _firstAccepts;
    private readonly List<uint> _accepts = new();

    private uint _maxTokenSymbol;

    public bool HasConflicts { get; private set; }

    public bool HasDefaultTransitions { get; private set; }

    public int StateCount { get; }

    public DfaWriter(int stateCount)
    {
        if (stateCount <= 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(stateCount));
        }

        StateCount = stateCount;

        _firstEdges = new int[stateCount];
        _defaultTransitions = new int[stateCount];
        _firstAccepts = new int[stateCount];
    }

    public void AddAccept(TokenSymbolHandle handle)
    {
        EnsureNotFinished();
        if (!handle.HasValue)
        {
            return;
        }

        _accepts.Add(handle.TableIndex);
        if (_accepts.Count - _firstAccepts[_currentState] > 1)
        {
            HasConflicts = true;
        }
        if (handle.TableIndex > _maxTokenSymbol)
        {
            _maxTokenSymbol = handle.TableIndex;
        }
    }

    public void AddEdge(TChar rangeFrom, TChar rangeTo, int targetState)
    {
        EnsureNotFinished();
        ValidateState(targetState);
        if (rangeFrom.CompareTo(rangeTo) > 0)
        {
            ThrowHelpers.ThrowArgumentException(nameof(rangeFrom), "Starting character is greater than ending character.");
        }

        _edges.Add((rangeFrom, rangeTo, targetState + 1));
    }

    public void AddEdgeFail(TChar rangeFrom, TChar rangeTo)
    {
        EnsureNotFinished();
        if (rangeFrom.CompareTo(rangeTo) > 0)
        {
            ThrowHelpers.ThrowArgumentException(nameof(rangeFrom), "Starting character is greater than ending character.");
        }

        _edges.Add((rangeFrom, rangeTo, 0));
    }

    private void EnsureFinished()
    {
        if (_currentState != StateCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Not all states have been written.");
        }
    }

    private void EnsureNotFinished()
    {
        if (_currentState == StateCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("All states have already been written.");
        }
    }

    public void FinishState()
    {
        EnsureNotFinished();
        int firstEdge = _firstEdges[_currentState];
        SortAndValidateEdgeRanges(firstEdge, _edges.Count - firstEdge);
        int firstAccept = _firstAccepts[_currentState];
        _accepts.Sort(firstAccept, _accepts.Count - firstAccept, null);

        _currentState++;
        if (_currentState < StateCount)
        {
            _firstEdges[_currentState] = _edges.Count;
            _firstAccepts[_currentState] = _accepts.Count;
        }
    }

    public void SetDefaultTransition(int targetState)
    {
        EnsureNotFinished();
        ValidateState(targetState);

        if (_defaultTransitions[_currentState] != 0)
            ThrowHelpers.ThrowInvalidOperationException("Default transition is already set for this state.");
        HasDefaultTransitions = true;
        _defaultTransitions[_currentState] = targetState + 1;
    }

    private void SortAndValidateEdgeRanges(int start, int count)
    {
        if (count <= 1)
        {
            return;
        }

        _edges.Sort(start, count, null);
        TChar k0 = _edges[start].KeyTo;
        for (int i = 1; i < count; i++)
        {
            (TChar keyFrom, TChar keyTo, _) = _edges[start + i];

            // We have tested that each edge's range is properly ordered when we added it.
            Debug.Assert(keyFrom.CompareTo(keyTo) <= 0);
            if (k0.CompareTo(keyFrom) >= 0)
            {
                ThrowHelpers.ThrowInvalidOperationException("DFA ranges overlap.");
            }

            k0 = keyTo;
        }
    }

    private void ValidateState(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= StateCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }

    public void WriteDfaData(IBufferWriter<byte> writer, int tokenSymbolCount)
    {
        EnsureFinished();
        if (_maxTokenSymbol > (uint)tokenSymbolCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot encode DFA; an invalid accept symbol has been written to it.");
        }

        writer.Write(StateCount);
        writer.Write(_edges.Count);
        if (HasConflicts)
        {
            writer.Write(_accepts.Count);
        }

        byte stateTargetSize = GetCompressedIndexSize(StateCount);
        byte edgeIndexSize = GetCompressedIndexSize(_edges.Count);
        byte tokenSymbolSize = GetCompressedIndexSize(tokenSymbolCount);

        foreach (int firstEdge in _firstEdges)
        {
            writer.WriteVariableSize((uint)firstEdge, edgeIndexSize);
        }
        foreach ((TChar keyFrom, _, _) in _edges)
        {
            writer.WriteChar(keyFrom);
        }
        foreach ((_, TChar keyTo, _) in _edges)
        {
            writer.WriteChar(keyTo);
        }
        foreach ((_, _, int targetState) in _edges)
        {
            writer.WriteVariableSize((uint)targetState, stateTargetSize);
        }

        if (HasConflicts)
        {
            byte acceptIndexSize = GetCompressedIndexSize(_accepts.Count);
            foreach (int firstAccept in _firstAccepts)
            {
                writer.WriteVariableSize((uint)firstAccept, acceptIndexSize);
            }
            foreach (uint handle in _accepts)
            {
                writer.WriteVariableSize(handle, tokenSymbolSize);
            }
        }
        else
        {
            for (int i = 0; i < _firstAccepts.Length; i++)
            {
                int firstAccept = _firstAccepts[i];
                int nextFirstAccept = i < _firstAccepts.Length - 1 ? _firstAccepts[i + 1] : _accepts.Count;

                uint handle = firstAccept < nextFirstAccept ? _accepts[firstAccept] : 0;
                writer.WriteVariableSize(handle, tokenSymbolSize);
            }
        }
    }

    public void WriteDefaultTransitions(IBufferWriter<byte> writer)
    {
        EnsureFinished();
        if (!HasDefaultTransitions)
        {
            ThrowHelpers.ThrowInvalidOperationException("DFA has no default transitions.");
        }

        byte stateTargetSize = GetCompressedIndexSize(StateCount);
        foreach (int state in _defaultTransitions)
        {
            writer.WriteVariableSize((uint)state, stateTargetSize);
        }
    }
}
