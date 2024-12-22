// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Characteristics of a nonterminal.
/// </summary>
[Flags]
public enum NonterminalAttributes : ushort
{
    /// <summary>
    /// No attributes are defined.
    /// </summary>
    None = 0,
    /// <summary>
    /// The nonterminal was not explicitly defined by the grammar author.
    /// </summary>
    Generated = 1 << 0
}
