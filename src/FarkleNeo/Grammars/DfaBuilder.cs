// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars;

internal class DfaBuilder<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private int _currentState;
    private bool _isFinished;

    private readonly int[] _firstEdges;
    // We use a tuple instead of DfaEdge to avoid writing our own comparer.
    private readonly List<(TChar KeyFrom, TChar KeyTo, int TargetState)> _edges = new();
    private readonly int[] _defaultTransitions;

    private readonly int[] _firstAccepts;
    private readonly List<TokenSymbolHandle> _accepts = new();
    private readonly HashSet<TokenSymbolHandle> _acceptsOfState = new();

    private uint _maxTokenSymbol;

    public bool HasConflicts { get; private set; }

    public bool HasDefaultTransitions { get; private set; }

    public int StateCount { get; }

    public DfaBuilder(int stateCount)
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
        if (!_acceptsOfState.Add(handle))
        {
            return;
        }

        _accepts.Add(handle);
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
        if (!_isFinished)
        {
            ThrowHelpers.ThrowInvalidOperationException("Builder is not finished, call Finish() first.");
        }
    }

    private void EnsureNotFinished()
    {
        if (_isFinished)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot modify a finished builder.");
        }
    }

    public void Finish()
    {
        if (_isFinished)
        {
            return;
        }
        if (_currentState != StateCount - 1)
        {
            ThrowHelpers.ThrowInvalidOperationException("Not all states have been defined.");
        }
        _isFinished = true;

        for (int i = 0; i < _firstEdges.Length - 1; i++)
        {
            int firstEdge = _firstEdges[i];
            int nextEdge = i < _firstEdges.Length - 1 ? _firstEdges[i + 1] : _edges.Count;
            SortAndValidateRanges(firstEdge, nextEdge - firstEdge, i);
        }
        SortAndValidateRanges(_firstEdges[^1], _edges.Count - _firstEdges[^1], StateCount - 1);

        void SortAndValidateRanges(int start, int count, int state)
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
                    ThrowOverlappingRanges(state);
                }

                k0 = keyTo;
            }

            static void ThrowOverlappingRanges(int state) =>
                ThrowHelpers.ThrowInvalidOperationException($"DFA ranges overlap on DFA state {state}");
        }
    }

    public void NextState()
    {
        EnsureNotFinished();
        if (_currentState == StateCount - 1)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot advance to next state; already at the last state.");
        }

        _currentState++;
        _firstEdges[_currentState] = _edges.Count;
        _firstAccepts[_currentState] = _accepts.Count;
        _acceptsOfState.Clear();
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

        byte stateIndexSize = GrammarTables.GetIndexSize(StateCount);
        byte edgeIndexSize = GrammarTables.GetIndexSize(_edges.Count);
        byte tokenSymbolSize = GrammarTables.GetIndexSize(tokenSymbolCount);

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
            writer.WriteVariableSize((uint)targetState, stateIndexSize);
        }

        if (HasConflicts)
        {
            byte acceptIndexSize = GrammarTables.GetIndexSize(_accepts.Count);
            foreach (int firstAccept in _firstAccepts)
            {
                writer.WriteVariableSize((uint)firstAccept, acceptIndexSize);
            }
            foreach (TokenSymbolHandle handle in _accepts)
            {
                writer.WriteVariableSize(handle.TableIndex, tokenSymbolSize);
            }
        }
        else
        {
            for (int i = 0; i < _firstAccepts.Length; i++)
            {
                int firstAccept = _firstAccepts[i];
                int nextFirstAccept = i < _firstAccepts.Length - 1 ? _firstAccepts[i + 1] : _accepts.Count;

                uint handle = firstAccept < nextFirstAccept ? _accepts[firstAccept].TableIndex : 0;
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

        byte stateIndexSize = GrammarTables.GetIndexSize(StateCount);
        foreach (int state in _defaultTransitions)
        {
            writer.WriteVariableSize((uint)state, stateIndexSize);
        }
    }
}
