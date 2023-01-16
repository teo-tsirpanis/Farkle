// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

internal enum TableKind : byte
{
    Grammar = 0,
    TokenSymbol = 1,
    Group = 2,
    GroupNesting = 3,
    Nonterminal = 4,
    Production = 5,
    ProductionMember = 6,
    StateMachine = 7,
    SpecialName = 8,

    PermanentlyHeldDoNotDisturb = 59
}
