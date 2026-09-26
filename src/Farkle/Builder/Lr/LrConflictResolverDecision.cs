// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents how a conflict contribution affects the formation of the dominant set.
/// </summary>
internal enum LrConflictResolverDecision
{
    /// <summary>
    /// The contribution should be ignored.
    /// </summary>
    Ignore,
    /// <summary>
    /// The contribution should be added to the dominant set.
    /// </summary>
    AddToDominantSet,
    /// <summary>
    /// The contribution should create a new dominant set.
    /// </summary>
    CreateNewDominantSet,
    /// <summary>
    /// The contribution should clear the dominant set.
    /// </summary>
    ClearDominantSet,
    /// <summary>
    /// The contribution has no precedence. All contributions, including those previously
    /// discarded, are included in the dominant set.
    /// </summary>
    NoPrecedence,
}
