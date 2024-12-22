// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

[Flags]
internal enum TableKinds : ulong
{
    Grammar = 1 << TableKind.Grammar,
    TokenSymbol = 1 << TableKind.TokenSymbol,
    Group = 1 << TableKind.Group,
    GroupNesting = 1 << TableKind.GroupNesting,
    Nonterminal = 1 << TableKind.Nonterminal,
    Production = 1 << TableKind.Production,
    ProductionMember = 1 << TableKind.ProductionMember,
    StateMachine = 1 << TableKind.StateMachine,
    SpecialName = 1 << TableKind.SpecialName,
    All = Grammar | TokenSymbol | Group | GroupNesting | Nonterminal | Production | ProductionMember | StateMachine | SpecialName,

    PermanentlyHeldDoNotDisturb = 1 << TableKind.PermanentlyHeldDoNotDisturb
}
