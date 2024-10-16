// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics.Builder;

/// <summary>
/// The kind of an <see cref="LrConflict"/>.
/// </summary>
public enum LrConflictKind
{
    /// <summary>
    /// The conflict is between a Shift and a Reduce action.
    /// </summary>
    ShiftReduce,
    /// <summary>
    /// The conflict is between two Reduce actions.
    /// </summary>
    ReduceReduce,
    /// <summary>
    /// The conflict is between an Accept and a Reduce action.
    /// </summary>
    /// <remarks>
    /// This is a special case of <see cref="ReduceReduce"/> and occurs very rarely.
    /// Accept-Reduce conflicts cannot be resolved with an
    /// <see cref="Farkle.Builder.OperatorPrecedence.OperatorScope"/>.
    /// </remarks>
    AcceptReduce
}
