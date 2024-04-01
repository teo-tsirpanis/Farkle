// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents the result of resolving an LR conflict.
/// </summary>
internal enum LrConflictResolverDecision
{
    /// <summary>
    /// The conflict cannot be resolved; include both actions in the state machine.
    /// </summary>
    CannotChoose,
    /// <summary>
    /// Choose the first option. This is Shift on Shift-Reduce conflicts and
    /// Reduce the first production on Reduce-Reduce conflicts.
    /// </summary>
    ChooseOption1,
    /// <summary>
    /// Choose the second option. This is Reduce on Shift-Reduce conflicts and
    /// Reduce the second production on Reduce-Reduce conflicts.
    /// </summary>
    ChooseOption2,
    /// <summary>
    /// Choose neither option.
    /// </summary>
    /// <remarks>
    /// This can be returned in Shift-Reduce conflicts if the terminal and the production
    /// have the same precedence and <see cref="OperatorPrecedence.AssociativityType.NonAssociative"/>
    /// associativity.
    /// </remarks>
    ChooseNeither
}
