// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using Farkle.Grammars.Writers;

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents an LR state machine that is ready to be written to a grammar.
/// </summary>
/// <remarks>
/// This is an abstract class whose descendants may either generate states from
/// a primary source, or transform existing state machines.
/// </remarks>
/// <seealso cref="ConflictResolvingLrStateMachine"/>
/// <seealso cref="LalrBuild.DefaultLrStateMachine"/>
internal abstract class LrStateMachine
{
    /// <summary>
    /// The number of states in the state machine.
    /// </summary>
    public abstract int StateCount { get; }

    /// <summary>
    /// Enumerates the <see cref="LrStateEntry"/>ies that correspond to a state.
    /// </summary>
    /// <param name="state">The index of the state.</param>
    public abstract IEnumerable<LrStateEntry> GetEntriesOfState(int state);

    /// <summary>
    /// Writes the state machine to an <see cref="LrWriter"/>.
    /// </summary>
    public LrWriter ToLrWriter()
    {
        int stateCount = StateCount;
        var writer = new LrWriter(StateCount);
        for (int i = 0; i < stateCount; i++)
        {
            foreach (LrStateEntry entry in GetEntriesOfState(i))
            {
                if (entry.IsTerminalAction(out TokenSymbolHandle terminal, out LrAction action))
                {
                    if (action.IsShift)
                    {
                        writer.AddShift(terminal, action.ShiftState);
                    }
                    else
                    {
                        // GetEntriesOfState should not return error actions.
                        Debug.Assert(action.IsReduce);
                        writer.AddReduce(terminal, action.ReduceProduction);
                    }
                }
                else if (entry.IsGoto(out NonterminalHandle nonterminal, out int destination))
                {
                    writer.AddGoto(nonterminal, destination);
                }
                else
                {
                    bool isEof = entry.IsEndOfFileAction(out LrEndOfFileAction eofAction);
                    Debug.Assert(isEof);
                    if (eofAction.IsReduce)
                    {
                        writer.AddEofReduce(eofAction.ReduceProduction);
                    }
                    else
                    {
                        // GetEntriesOfState should not return error actions.
                        Debug.Assert(eofAction.IsAccept);
                        writer.AddEofAccept();
                    }
                }
            }
            writer.FinishState();
        }
        return writer;
    }
}
