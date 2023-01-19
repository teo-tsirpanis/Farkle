// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using Farkle.Buffers;
using Farkle.Grammars;
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
        int ProductionMember, int StateMachine, int SpecialName, bool SmallBlobHeap, bool SmallStringHeap)
    {
        public TableKinds GetTablesPresent()
        {
            TableKinds presentTables = TableKinds.Grammar;
            Update(TokenSymbol, TableKinds.TokenSymbol);
            Update(Group, TableKinds.Group);
            Update(GroupNesting, TableKinds.GroupNesting);
            Update(Nonterminal, TableKinds.Nonterminal);
            Update(Production, TableKinds.Production);
            Update(ProductionMember, TableKinds.ProductionMember);
            Update(StateMachine, TableKinds.StateMachine);
            Update(SpecialName, TableKinds.SpecialName);

            return presentTables;

            void Update(int count, TableKinds tableKind)
            {
                if (count > 0) presentTables |= tableKind;
            }
        }

        public byte GetHeapSizes() => (byte)((SmallStringHeap ? 1 : 0) | (SmallBlobHeap ? 2 : 0));

        public byte[] ToArray()
        {
            using var bw = new PooledSegmentBufferWriter<byte>();

            TableKinds tablesPresent = GetTablesPresent();
            int tableCount = PopCount((ulong)tablesPresent);
            byte heapSizes = GetHeapSizes();

            bw.Write((ulong)tablesPresent);

            return bw.ToArray();
        }

        // We can't easily use the Compatibility classes
        // because we are also importing Farkle's, so we
        // have to write our own implementation.
        static int PopCount(ulong value)
        {
            int count = 0;
            while (value > 0)
            {
                if (value % 2 == 0)
                {
                    count++;
                }
                value /= 2;
            }
            return count;
        }
    }
}
