// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.Writers;

internal sealed class LrWriter
{
    private int _currentState;
    private bool _isFinished;

    private readonly List<(uint TerminalIndex, LrTerminalAction Action)> _actions = new();
    private readonly int[] _firstActions;
    private uint _maxTerminal, _maxProduction;

    private readonly int[] _firstEofActions;
    private readonly List<LrEndOfFileAction> _eofActions = new();

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

        AddAction(terminal, LrTerminalAction.CreateReduce(production));

        if (production.TableIndex > _maxProduction)
        {
            _maxProduction = production.TableIndex;
        }
    }

    public void AddShift(TokenSymbolHandle terminal, int state)
    {
        EnsureNotFinished();
        ValidateState(state);

        AddAction(terminal, LrTerminalAction.CreateShift(state));
    }

    private void AddAction(TokenSymbolHandle terminal, LrTerminalAction action)
    {
        if (terminal.TableIndex > _maxTerminal)
        {
            _maxTerminal = terminal.TableIndex;
        }
        if (_actions.Count - _firstActions[_currentState] > 1)
        {
            HasConflicts = true;
        }

        _actions.Add((terminal.TableIndex, action));
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
        if (_eofActions.Count - _firstEofActions[_currentState] > 1)
        {
            HasConflicts = true;
        }
        _eofActions.Add(action);
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
    }

    public void NextState()
    {
        EnsureNotFinished();
        if (_currentState == StateCount - 1)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot advance to next state; already at the last state.");
        }

        Sort(_firstActions, _actions);
        Sort(_firstEofActions, _eofActions);
        Sort(_firstGotos, _gotos);

        _currentState++;
        _firstActions[_currentState] = _actions.Count;
        _firstEofActions[_currentState] = _eofActions.Count;
        _firstGotos[_currentState] = _gotos.Count;

        void Sort<T>(int[] firstItems, List<T> items)
        {
            int firstItem = firstItems[_currentState];
            items.Sort(firstItem, items.Count - firstItem, null);
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

        byte stateIndexSize = StateMachineUtilities.GetIndexSize(StateCount);
        byte actionIndexSize = StateMachineUtilities.GetIndexSize(_actions.Count);
        byte actionSize = LrTerminalAction.GetEncodedSize(StateCount, productionCount);
        byte eofActionSize = LrEndOfFileAction.GetEncodedSize(productionCount);
        byte gotoIndexSize = StateMachineUtilities.GetIndexSize(_gotos.Count);
        byte nonterminalIndexSize = GrammarTables.GetIndexSize(nonterminalCount);
        byte tokenSymbolIndexSize = GrammarTables.GetIndexSize(tokenSymbolCount);

        foreach (int firstAction in _firstActions)
        {
            writer.WriteVariableSize((uint)firstAction, actionIndexSize);
        }
        foreach ((uint terminal, _) in _actions)
        {
            writer.WriteVariableSize(terminal, tokenSymbolIndexSize);
        }
        foreach ((_, LrTerminalAction action) in _actions)
        {
            writer.WriteVariableSize((uint)action.Value, actionSize);
        }
        if (HasConflicts)
        {
            byte eofActionIndexSize = StateMachineUtilities.GetIndexSize(_eofActions.Count);
            foreach (int firstEofAction in _firstEofActions)
            {
                writer.WriteVariableSize((uint)firstEofAction, eofActionIndexSize);
            }
            foreach (LrEndOfFileAction eofAction in _eofActions)
            {
                writer.WriteVariableSize(eofAction.Value, eofActionSize);
            }
        }
        else
        {
            for (int i = 0; i < _firstEofActions.Length; i++)
            {
                int firstEofAction = _firstEofActions[i];
                int nextFirstEofAction = i < _firstEofActions.Length - 1 ? _firstEofActions[i + 1] : _eofActions.Count;

                LrEndOfFileAction action = firstEofAction < nextFirstEofAction ? _eofActions[firstEofAction] : LrEndOfFileAction.Error;
                writer.WriteVariableSize(action.Value, tokenSymbolIndexSize);
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
