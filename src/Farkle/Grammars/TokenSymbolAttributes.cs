// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Characteristics of a token symbol.
/// </summary>
[Flags]
public enum TokenSymbolAttributes : uint
{
    /// <summary>
    /// No attributes are defined.
    /// </summary>
    None = 0,
    /// <summary>
    /// The token symbol can exist in the right-hand side of a production.
    /// </summary>
    Terminal = 1 << 0,
    /// <summary>
    /// The token symbol can start a group.
    /// </summary>
    // Unlike other flags in earlier iterations of the grammar format that were removed,
    // this flag must stay. Checking if a token symbol starts a group is pretty expensive
    // and happens on every token. We can skip this check with this flag.
    GroupStart = 1 << 1,
    /// <summary>
    /// The token symbol must be skipped by parsers if encountered in an unexpected place in the input.
    /// </summary>
    Noise = 1 << 2,
    /// <summary>
    /// The token symbol should not be displayed by parsers in the expected tokens list in case of a syntax error.
    /// </summary>
    Hidden = 1 << 3,
    /// <summary>
    /// The token symbol was not explicitly defined by the grammar author.
    /// </summary>
    Generated = 1 << 4
}
