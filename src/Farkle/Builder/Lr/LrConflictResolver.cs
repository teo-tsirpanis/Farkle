// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic to resolve LR conflicts.
/// </summary>
internal abstract class LrConflictResolver
{
    /// <summary>
    /// Returns whether the given terminal or production has precedence and associativity information.
    /// </summary>
    /// <param name="symbol">The symbol to check.</param>
    public abstract bool HasPrecedenceInfo(EntityHandle symbol);

    /// <summary>
    /// Resolves a Shift-Reduce conflict.
    /// </summary>
    /// <param name="shiftTerminal">The terminal on which the action will be taken.</param>
    /// <param name="reduceProduction">The production to reduce.</param>
    public abstract LrConflictResolverDecision ResolveShiftReduceConflict(TokenSymbolHandle shiftTerminal, ProductionHandle reduceProduction);

    /// <summary>
    /// Resolves a Reduce-Reduce conflict.
    /// </summary>
    /// <param name="production1">The first possible production to reduce.</param>
    /// <param name="production2">The second possible production to reduce.</param>
    /// <remarks>
    /// This method may not return <see cref="LrConflictResolverDecision.ChooseNeither"/>.
    /// When resolving Reduce-Reduce conflicts, the productions' associativity are not
    /// taken into account.
    /// </remarks>
    public abstract LrConflictResolverDecision ResolveReduceReduceConflict(ProductionHandle production1, ProductionHandle production2);

    public LrConflictResolverDecision ResolveConflict(TokenSymbolHandle terminal, LrAction action1, LrAction action2)
    {
        switch (action1.IsShift, action2.IsShift)
        {
            case (true, true):
                Debug.Fail("Shift/Shift conflict is not possible");
                return LrConflictResolverDecision.ChooseOption1;
            case (true, false):
                return ResolveShiftReduceConflict(terminal, action2.ReduceProduction);
            case (false, true):
                return Invert(ResolveShiftReduceConflict(terminal, action1.ReduceProduction));
            case (false, false):
                return ResolveReduceReduceConflict(action1.ReduceProduction, action2.ReduceProduction);
        }

        static LrConflictResolverDecision Invert(LrConflictResolverDecision decision) => decision switch
        {
            LrConflictResolverDecision.ChooseOption1 => LrConflictResolverDecision.ChooseOption2,
            LrConflictResolverDecision.ChooseOption2 => LrConflictResolverDecision.ChooseOption1,
            _ => decision
        };
    }

    public LrConflictResolverDecision ResolveEndOfFileConflict(LrEndOfFileAction action1, LrEndOfFileAction action2)
    {
        if (action1.IsAccept || action2.IsAccept)
        {
            Debug.Assert(!(action1.IsAccept && action2.IsAccept), "Accept/Accept conflict is not possible");
            // Accept/Reduce conflicts cannot be resolved.
            return LrConflictResolverDecision.CannotChoose;
        }
        return ResolveReduceReduceConflict(action1.ReduceProduction, action2.ReduceProduction);
    }
}
