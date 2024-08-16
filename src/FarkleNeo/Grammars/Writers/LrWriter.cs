// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;
using System.Runtime.CompilerServices;
using static Farkle.Grammars.GrammarUtilities;

namespace Farkle.Grammars.Writers;

internal sealed class LrWriter
{
    private int _currentState;

    private readonly List<(uint TerminalIndex, int Action)> _actions = new();
    private readonly int[] _firstActions;
    private uint _maxTerminal, _maxProduction;

    private readonly int[] _firstEofActions;
    private readonly List<uint> _eofActions = new();

    private readonly List<(uint NonterminalIndex, int State)> _gotos = new();
    private readonly int[] _firstGotos;
    private uint _maxNonterminal;

    public int StateCount { get; }

    public bool HasConflicts { get; private set; }

    public LrWriter(int stateCount)
    {
        if (stateCount <= 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(stateCount));
        }

        StateCount = stateCount;

        _firstActions = new int[stateCount];
        _firstEofActions = new int[stateCount];
        _firstGotos = new int[stateCount];
    }

    public void AddReduce(TokenSymbolHandle terminal, ProductionHandle production)
    {
        EnsureNotFinished();

        AddAction(terminal, LrAction.CreateReduce(production));

        if (production.TableIndex > _maxProduction)
        {
            _maxProduction = production.TableIndex;
        }
    }

    public void AddShift(TokenSymbolHandle terminal, int state)
    {
        EnsureNotFinished();
        ValidateState(state);

        AddAction(terminal, LrAction.CreateShift(state));
    }

    private void AddAction(TokenSymbolHandle terminal, LrAction action)
    {
        if (terminal.TableIndex > _maxTerminal)
        {
            _maxTerminal = terminal.TableIndex;
        }

        _actions.Add((terminal.TableIndex, action.Value));
    }

    public void AddEofAccept()
    {
        EnsureNotFinished();

        AddEofAction(LrEndOfFileAction.Accept);
    }

    public void AddEofReduce(ProductionHandle production)
    {
        EnsureNotFinished();

        AddEofAction(LrEndOfFileAction.CreateReduce(production));

        if (production.TableIndex > _maxProduction)
        {
            _maxProduction = production.TableIndex;
        }
    }

    private void AddEofAction(LrEndOfFileAction action)
    {
        _eofActions.Add(action.Value);
        if (_eofActions.Count - _firstEofActions[_currentState] > 1)
        {
            HasConflicts = true;
        }
    }

    public void AddGoto(NonterminalHandle nonterminal, int state)
    {
        EnsureNotFinished();
        ValidateState(state);

        if (nonterminal.TableIndex > _maxNonterminal)
        {
            _maxNonterminal = nonterminal.TableIndex;
        }

        _gotos.Add((nonterminal.TableIndex, state));
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
        HasConflicts |= SortAndCheckForConflicts(_firstActions, _actions);
        int firstEofAction = _firstEofActions[_currentState];
        _eofActions.Sort(firstEofAction, _eofActions.Count - firstEofAction, null);
        if (SortAndCheckForConflicts(_firstGotos, _gotos))
        {
            ThrowHelpers.ThrowInvalidOperationException("Conflicts on GOTO transitions are not allowed.");
        }

        _currentState++;
        if (_currentState < StateCount)
        {
            _firstActions[_currentState] = _actions.Count;
            _firstEofActions[_currentState] = _eofActions.Count;
            _firstGotos[_currentState] = _gotos.Count;
        }

        bool SortAndCheckForConflicts<T>(int[] firstItems, List<(uint Key, T)> items)
        {
            int firstItem = firstItems[_currentState];
            items.Sort(firstItem, items.Count - firstItem, null);

            if (firstItem != items.Count)
            {
                uint previousKey = items[firstItem].Key;
                for (int i = firstItem + 1; i < items.Count; i++)
                {
                    uint key = items[i].Key;
                    if (key == previousKey)
                    {
                        return true;
                    }
                    previousKey = key;
                }
            }
            return false;
        }
    }

    private void ValidateState(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= StateCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }

    public void WriteData(IBufferWriter<byte> writer, int tokenSymbolCount, int terminalCount, int productionCount, int nonterminalCount)
    {
        EnsureFinished();
        if (_maxTerminal > (uint)terminalCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot encode LR state machine; an invalid terminal has been written to it.");
        }
        if (_maxProduction > (uint)productionCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot encode LR state machine; an invalid production has been written to it.");
        }
        if (_maxNonterminal > (uint)nonterminalCount)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot encode LR state machine; an invalid nonterminal has been written to it.");
        }

        writer.Write(StateCount);
        writer.Write(_actions.Count);
        writer.Write(_gotos.Count);
        if (HasConflicts)
        {
            writer.Write(_eofActions.Count);
        }

        byte stateIndexSize = GetCompressedIndexSize(StateCount);
        byte actionIndexSize = GetCompressedIndexSize(_actions.Count);
        byte actionSize = GetLrActionEncodedSize(StateCount, productionCount);
        byte eofActionSize = GetCompressedIndexSize(productionCount);
        byte gotoIndexSize = GetCompressedIndexSize(_gotos.Count);
        byte nonterminalIndexSize = GetCompressedIndexSize(nonterminalCount);
        byte tokenSymbolIndexSize = GetCompressedIndexSize(tokenSymbolCount);

        foreach (int firstAction in _firstActions)
        {
            writer.WriteVariableSize((uint)firstAction, actionIndexSize);
        }
        foreach ((uint terminal, _) in _actions)
        {
            writer.WriteVariableSize(terminal, tokenSymbolIndexSize);
        }
        foreach ((_, int action) in _actions)
        {
            writer.WriteVariableSize(action, actionSize);
        }
        if (HasConflicts)
        {
            byte eofActionIndexSize = GetCompressedIndexSize(_eofActions.Count);
            foreach (int firstEofAction in _firstEofActions)
            {
                writer.WriteVariableSize((uint)firstEofAction, eofActionIndexSize);
            }
            foreach (uint eofAction in _eofActions)
            {
                writer.WriteVariableSize(eofAction, eofActionSize);
            }
        }
        else
        {
            for (int i = 0; i < _firstEofActions.Length; i++)
            {
                int firstEofAction = _firstEofActions[i];
                int nextFirstEofAction = i < _firstEofActions.Length - 1 ? _firstEofActions[i + 1] : _eofActions.Count;

                uint action = firstEofAction < nextFirstEofAction ? _eofActions[firstEofAction] : LrEndOfFileAction.Error.Value;
                writer.WriteVariableSize(action, eofActionSize);
            }
        }
        foreach (int firstGoto in _firstGotos)
        {
            writer.WriteVariableSize((uint)firstGoto, gotoIndexSize);
        }
        foreach ((uint nonterminal, _) in _gotos)
        {
            writer.WriteVariableSize(nonterminal, nonterminalIndexSize);
        }
        foreach ((_, int state) in _gotos)
        {
            writer.WriteVariableSize((uint)state, stateIndexSize);
        }
    }
}
