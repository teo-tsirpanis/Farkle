// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Describes the circumstances under which the builder will emit default reductions in the grammar's parsing tables.
/// </summary>
/// <seealso cref="GrammarBuilderExtensions.WithDefaultReductions"/>
/// <seealso cref="GrammarBuilderExtensions.WithDefaultReductions{T}"/>
public enum DefaultReductionsOption
{
    /// <summary>
    /// The builder never emits default reductions.
    /// </summary>
    None,
    /// <summary>
    /// The builder may emit default reductions only on consistent states.
    /// </summary>
    /// <remarks>
    /// A consistent state is one in which all Reduce actions reduce the same production.
    /// </remarks>
    ConsistentStates,
    /// <summary>
    /// The builder may emit default reductions on all states.
    /// </summary>
    All,
}
