// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Specifies the case sensitivity of a grammar to be built.
/// </summary>
/// <seealso cref="GrammarBuilderExtensions.CaseSensitive(IGrammarBuilder, CaseSensitivity)"/>
public enum CaseSensitivity
{
    /// <summary>
    /// The entire grammar is case sensitive.
    /// </summary>
    /// <remarks>
    /// This is the default value since Farkle 7.
    /// </remarks>
    CaseSensitive,
    /// <summary>
    /// Only the literals of the grammar are case insensitive and the other terminals are case
    /// sensitive.
    /// </summary>
    /// <remarks>
    /// Literals of the grammar are considered the terminals created by <see cref="Terminal.Literal"/>
    /// as well as the start and end symbols of groups and comments.
    /// </remarks>
    LiteralsCaseInsensitive,
    /// <summary>
    /// The entire grammar is case insensitive.
    /// </summary>
    /// <remarks>
    /// This was the default value in versions of Farkle prior to 7,
    /// but was changed to <see cref="CaseSensitive"/> for performance reasons.
    /// </remarks>
    CaseInsensitive
}
