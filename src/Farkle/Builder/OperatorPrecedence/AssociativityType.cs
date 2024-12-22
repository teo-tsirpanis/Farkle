// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder.OperatorPrecedence;

/// <summary>
/// Represents the possible kinds of associativity of an <see cref="AssociativityGroup"/>.
/// </summary>
public enum AssociativityType
{
    /// <summary>
    /// The group's symbols are non-associative.
    /// </summary>
    /// <remarks>
    /// Shift-Reduce conflicts will be resolved in favor of neither, failing
    /// with a syntax error at parse time.
    /// </remarks>
    NonAssociative,
    /// <summary>
    /// The group's symbols are left-associative.
    /// </summary>
    /// <remarks>
    /// Shift-Reduce conflicts will be resolved in favor of Reduce.
    /// </remarks>
    LeftAssociative,
    /// <summary>
    /// The group's symbols are right-associative.
    /// </summary>
    /// <remarks>
    /// Shift-Reduce conflicts will be resolved in favor of Shift.
    /// </remarks>
    RightAssociative,
    /// <summary>
    /// The group's symbols have no associativity; only precedence.
    /// </summary>
    /// <remarks>
    /// Shift-Reduce conflicts will not be resolved and will fail the build.
    /// </remarks>
    PrecedenceOnly
}
