// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;
using System.Diagnostics;
using static Farkle.Grammars.GrammarUtilities;

namespace Farkle.Grammars.Writers;

internal class DfaWriter<TChar> where TChar : unmanaged, IComparable<TChar>
{
    private readonly List<int> _firstEdges = [];
    // We use a tuple instead of DfaEdge to avoid writing our own comparer.
    private readonly List<(TChar KeyFrom, TChar KeyTo, int TargetState)> _edges = [];
    private int _pendingFirstEdge;

    private readonly List<int> _defaultTransitions = [];

    private readonly List<int> _firstAccepts = [];
    private readonly List<uint> _accepts = [];
    private int _pendingFirstAccept;

    private int _maxState;

    private uint _maxTokenSymbol;

    private bool HasUnfinishedState => _pendingFirstEdge != _edges.Count || _pendingFirstAccept != _accepts.Count;

    public bool HasConflicts { get; private set; }

    public bool HasDefaultTransitions { get; private set; }

    public int StateCount => _firstEdges.Count;

    public IEnumerable<TokenSymbolHandle> EnumerateAcceptSymbols()
    {
        foreach (var accept in _accepts)
        {
            yield return new(accept);
        }
    }

    public void AddAccept(TokenSymbolHandle handle)
    {
        if (!handle.HasValue)
        {
            return;
        }

        _accepts.Add(handle.TableIndex);
        if (_accepts.Count - _pendingFirstAccept > 1)
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
        if (rangeFrom.CompareTo(rangeTo) > 0)
        {
            ThrowHelpers.ThrowArgumentException(nameof(rangeFrom), "Starting character is greater than ending character.");
        }
        if (targetState > _maxState)
        {
            _maxState = targetState;
        }

        _edges.Add((rangeFrom, rangeTo, targetState + 1));
    }

    public void AddEdgeFail(TChar rangeFrom, TChar rangeTo)
    {
        if (rangeFrom.CompareTo(rangeTo) > 0)
        {
            ThrowHelpers.ThrowArgumentException(nameof(rangeFrom), "Starting character is greater than ending character.");
        }

        _edges.Add((rangeFrom, rangeTo, 0));
    }

    private void EnsureFinished()
    {
        if (HasUnfinishedState || _maxState > StateCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Not all states have been written.");
        }
    }

    public void FinishState(int? defaultTransition = null)
    {
        SortAndValidateEdgeRanges(_pendingFirstEdge, _edges.Count - _pendingFirstEdge);
        _accepts.Sort(_pendingFirstAccept, _accepts.Count - _pendingFirstAccept, null);

        _firstEdges.Add(_pendingFirstEdge);
        _pendingFirstEdge = _edges.Count;
        _firstAccepts.Add(_pendingFirstAccept);
        _pendingFirstAccept = _accepts.Count;

        if (defaultTransition is { } dt)
        {
            if (dt > _maxState)
            {
                _maxState = dt;
            }

            HasDefaultTransitions = true;
            _defaultTransitions.Add(dt + 1);
        }
        else
        {
            _defaultTransitions.Add(0);
        }
        Debug.Assert(!HasUnfinishedState);
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
            for (int i = 0; i < _firstAccepts.Count; i++)
            {
                int firstAccept = _firstAccepts[i];
                int nextFirstAccept = i < _firstAccepts.Count - 1 ? _firstAccepts[i + 1] : _accepts.Count;

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
