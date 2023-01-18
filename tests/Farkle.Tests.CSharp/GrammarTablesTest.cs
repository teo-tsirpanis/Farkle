// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using FsCheck;
using FsCheck.Fluent;

namespace Farkle.Tests.CSharp;

internal class GrammarTablesTest
{
    private static readonly Gen<bool> GenBoolean = ArbMap.Default.GeneratorFor<bool>();

    private static readonly Gen<int> GenTableSize =
        Gen.OneOf(Gen.Constant(0), Gen.Choose(1, 0xFE), Gen.Choose(0xFF, 0xFFFE), Gen.Choose(0xFFFF, 0xFFFFE));

    private static readonly Gen<TableCounts> GenTableCounts =
        from tokenSymbol in GenTableSize
        from @group in GenTableSize
        from groupNesting in GenTableSize
        from nonterminal in GenTableSize
        from production in GenTableSize
        from productionMember in GenTableSize
        from stateMachine in GenTableSize
        from specialName in GenTableSize
        from smallBlobHeap in GenBoolean
        from smallStringHeap in GenBoolean
        select new TableCounts(tokenSymbol, @group, groupNesting, nonterminal, production,
            productionMember, stateMachine, specialName, smallBlobHeap, smallStringHeap);

    private sealed record TableCounts(int TokenSymbol, int Group, int GroupNesting, int Nonterminal, int Production,
        int ProductionMember, int StateMachine, int SpecialName, bool SmallBlobHeap, bool SmallStringHeap);
}
