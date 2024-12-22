// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;

namespace Farkle.Builder.Lr;

/// <summary>
/// Resolves conflicts from an <see cref="LrStateMachine"/>.
/// </summary>
internal sealed class ConflictResolvingLrStateMachine(LrStateMachine stateMachine, LrConflictResolver conflictResolver) : LrStateMachine
{
    public LrStateMachine InnerStateMachine { get; } = stateMachine;

    public LrConflictResolver ConflictResolver { get; } = conflictResolver;

    public override int StateCount => InnerStateMachine.StateCount;

    private LrConflictResolverDecision ResolveConflict(TokenSymbolHandle terminal, LrAction action1, LrAction action2)
    {
        switch (action1.IsShift, action2.IsShift)
        {
            case (true, true):
                Debug.Fail("Shift/Shift conflict is not possible");
                return LrConflictResolverDecision.ChooseOption1;
            case (true, false):
                return ConflictResolver.ResolveShiftReduceConflict(terminal, action2.ReduceProduction);
            case (false, true):
                return Invert(ConflictResolver.ResolveShiftReduceConflict(terminal, action1.ReduceProduction));
            case (false, false):
                return ConflictResolver.ResolveReduceReduceConflict(action1.ReduceProduction, action2.ReduceProduction);
        }

        static LrConflictResolverDecision Invert(LrConflictResolverDecision decision) => decision switch
        {
            LrConflictResolverDecision.ChooseOption1 => LrConflictResolverDecision.ChooseOption2,
            LrConflictResolverDecision.ChooseOption2 => LrConflictResolverDecision.ChooseOption1,
            _ => decision
        };
    }

    private LrConflictResolverDecision ResolveEndOfFileConflict(LrEndOfFileAction action1, LrEndOfFileAction action2)
    {
        if (action1.IsAccept || action2.IsAccept)
        {
            Debug.Assert(!(action1.IsAccept && action2.IsAccept), "Accept/Accept conflict is not possible");
            // Accept/Reduce conflicts cannot be resolved.
            return LrConflictResolverDecision.CannotChoose;
        }
        return ConflictResolver.ResolveReduceReduceConflict(action1.ReduceProduction, action2.ReduceProduction);
    }

    public override IEnumerable<LrStateEntry> GetEntriesOfState(int state)
    {
        // To support all possible scenarios like multi-way conflicts, we have
        // to read the entire actions of the state before returning any of them.
        // Consider a R/R conflict on productions a, b and c. We must choose the
        // production(s) with the highest precedence. If the resolver returns
        // CannotChoose for two productions, it means they have equal precedence.
        // For example, if a and b have equal precedence but greater than c,
        // we write only a and b to the grammar.
        // While in practice the base state machine emits shifts before reduces,
        // this algorithm should work for any order of actions. For example,
        // consider shift action s and reduce actions r1 and r2 on the same
        // terminal, with the same precedence and left associativity. If r1
        // comes first and s second, r1 will prevail, and then when r2 comes
        // they will have the same precedence and result in an R/R conflict
        // as expected. If the actions were right-associative, s would prevail
        // over r1 and r2 regardless of the order.
        // We use sparse data structures like Dictionary and HashSet instead
        // of array and BitArrayNeo because each state usually has actions on
        // few terminals.

        // This dictionary contains the current dominant actions for each terminal.
        // It gets reset when a new action has a higher precedence than the existing one.
        Dictionary<TokenSymbolHandle, List<LrAction>> existingActions = [];
        // This set contains the terminals whose dominant actions are in a non-associative
        // group, as determined by the resolver returning ChooseNeither. Consider shift
        // action s and reduce actions r1 and r2 on the same terminal. If the resolver
        // returns ChooseNeither for s and r1 and not ChooseOption2 for s and r2 (or r1
        // and r2), the dominant actions are s and r1. Because they are in a non-associative
        // group, when we return the states at the end we will entirely skip this terminal.
        HashSet<TokenSymbolHandle> terminalsWithChooseNeither = [];
        List<LrEndOfFileAction>? existingEndOfFileActions = null;
        foreach (LrStateEntry entry in InnerStateMachine.GetEntriesOfState(state))
        {
            if (entry.IsGoto(out _, out _))
            {
                // Return gotos unchanged. They are not subject to conflicts.
                yield return entry;
                continue;
            }
            if (entry.IsTerminalAction(out var symbol, out var action))
            {
                if (existingActions.TryGetValue(symbol, out var existingActionsOfTerminal) && existingActionsOfTerminal is [.., var existingAction])
                {
                    switch (ResolveConflict(symbol, existingAction, action))
                    {
                        // The new action has a lower priority. Keep the existing actions.
                        case LrConflictResolverDecision.ChooseOption1:
                            continue;
                        // The new action has a higher priority. Discard the existing actions.
                        case LrConflictResolverDecision.ChooseOption2:
                            existingActionsOfTerminal.Clear();
                            existingActionsOfTerminal.Add(action);
                            terminalsWithChooseNeither.Remove(symbol);
                            break;
                        // The two actions have the same priority and neither should be chosen.
                        // Mark the terminal so that we don't emit its actions.
                        // We don't have to add the new action to the list of dominant actions;
                        // this associativity group is already represented in the list.
                        case LrConflictResolverDecision.ChooseNeither:
                            terminalsWithChooseNeither.Add(symbol);
                            break;
                        // The two actions have the same priority. Keep both.
                        case LrConflictResolverDecision.CannotChoose:
                            existingActionsOfTerminal.Add(action);
                            break;
                    }
                }
                else
                {
                    // This is the first action for this terminal.
                    existingActions.Add(symbol, [action]);
                }
                continue;
            }
            bool isEof = entry.IsEndOfFileAction(out var endOfFileAction);
            Debug.Assert(isEof);
            if (existingEndOfFileActions is [.., var existingEndOfFileAction])
            {
                switch (ResolveEndOfFileConflict(existingEndOfFileAction, endOfFileAction))
                {
                    // The new action has a lower priority. Keep the existing actions.
                    case LrConflictResolverDecision.ChooseOption1:
                        continue;
                    // The new action has a higher priority. Discard the existing actions.
                    case LrConflictResolverDecision.ChooseOption2:
                        existingEndOfFileActions.Clear();
                        existingEndOfFileActions.Add(endOfFileAction);
                        break;
                    // Conflict resolvers should not return ChooseNeither for Reduce-Reduce
                    // conflicts because these are not using associativity, only precedence.
                    case LrConflictResolverDecision.ChooseNeither:
                        Debug.Fail("ChooseNeither is not allowed in EndOfFile actions");
                        break;
                    // The two actions have the same priority. Keep both.
                    case LrConflictResolverDecision.CannotChoose:
                        existingEndOfFileActions.Add(endOfFileAction);
                        break;
                }
            }
            else
            {
                // This is the first action for EOF.
                existingEndOfFileActions = [endOfFileAction];
            }
        }

        // Return the dominant actions.
        foreach (var kvp in existingActions)
        {
            TokenSymbolHandle symbol = kvp.Key;
            List<LrAction> actions = kvp.Value;
            // Skip terminals whose dominant actions are in a non-associative group.
            if (terminalsWithChooseNeither.Contains(symbol))
            {
                continue;
            }
            foreach (var action in actions)
            {
                yield return LrStateEntry.Create(symbol, action);
            }
        }

        // Return the dominant EOF actions.
        if (existingEndOfFileActions is not null)
        {
            foreach (var action in existingEndOfFileActions)
            {
                yield return LrStateEntry.CreateEndOfFileAction(action);
            }
        }
    }
}
