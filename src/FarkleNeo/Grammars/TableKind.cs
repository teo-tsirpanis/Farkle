// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Describes the kind of an <see cref="EntityHandle"/>.
/// </summary>
/// <seealso href="https://github.com/teo-tsirpanis/Farkle/blob/mainstream/designs/7.0/grammar-file-format-spec.md"/>
internal enum TableKind : byte
{
    /// <summary>
    /// The handle points to the Grammar table.
    /// </summary>
    Grammar = 0,
    /// <summary>
    /// The handle points to the TokenSymbol table.
    /// </summary>
    TokenSymbol = 1,
    /// <summary>
    /// The handle points to the Group table.
    /// </summary>
    Group = 2,
    /// <summary>
    /// The handle points to the GroupNesting table.
    /// </summary>
    GroupNesting = 3,
    /// <summary>
    /// The handle points to the Nonterminal table.
    /// </summary>
    Nonterminal = 4,
    /// <summary>
    /// The handle points to the Production table.
    /// </summary>
    Production = 5,
    /// <summary>
    /// The handle points to the ProductionMember table.
    /// </summary>
    ProductionMember = 6,
    /// <summary>
    /// The handle points to the StateMachine table.
    /// </summary>
    StateMachine = 7,
    /// <summary>
    /// The handle points to the SpecialName table.
    /// </summary>
    SpecialName = 8,

    PermanentlyHeldDoNotDisturb = 59
}
