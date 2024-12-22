// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic to resolve LR conflicts.
/// </summary>
internal abstract class LrConflictResolver
{
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
}
