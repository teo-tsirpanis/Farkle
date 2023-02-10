// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Farkle.Grammars;

[StructLayout(LayoutKind.Auto)]
internal readonly struct GrammarTables
{
    // For each table we store:
    // - the number of rows
    // - the size of each row
    // - the (absolute - from the start of the grammar file)
    //   offset to the first row of each table column, AKA base.
    //   PERF: We could optimize memory footprint by storing each
    //   column's displacement from the start of the row.
    // To get the value of a column, we add to its base
    // the product of the row index and the row size.

    // The Grammar table has exactly one row so we just store the offset to each column.
    public readonly int GrammarNameOffset, GrammarStartSymbolOffset, GrammarFlagsOffset;

    public readonly int TokenSymbolRowCount;
    public readonly byte TokenSymbolRowSize;
    public readonly int TokenSymbolNameBase, TokenSymbolFlagsBase;

    public readonly int GroupRowCount;
    public readonly byte GroupRowSize;
    public readonly int GroupNameBase, GroupContainerBase, GroupFlagsBase, GroupStartBase, GroupEndBase, GroupFirstNestingBase;

    public readonly int GroupNestingRowCount;
    // PERF: We could calculate it on-demand; this is a single-column row.
    public readonly byte GroupNestingRowSize;
    public readonly int GroupNestingGroupBase;

    public readonly int NonterminalRowCount;
    public readonly byte NonterminalRowSize;
    public readonly int NonterminalNameBase, NonterminalFlagsBase, NonterminalFirstProductionBase;

    public readonly int ProductionRowCount;
    // PERF: We could calculate it on-demand; this is a single-column row.
    public readonly byte ProductionRowSize;
    public readonly int ProductionFirstMemberBase;

    public readonly int ProductionMemberRowCount;
    // PERF: We could calculate it on-demand; this is a single-column row.
    public readonly byte ProductionMemberRowSize;
    public readonly int ProductionMemberMemberBase;

    public readonly int StateMachineRowCount;
    public readonly byte StateMachineRowSize;
    public readonly int StateMachineKindBase, StateMachineDataBase;

    public readonly int SpecialNameRowCount;
    public readonly byte SpecialNameRowSize;
    public readonly int SpecialNameNameBase, SpecialNameSymbolBase;

    private readonly HeapSizes _heapSizes;

    public byte BlobHeapIndexSize => (byte)((_heapSizes & HeapSizes.BlobHeapSmall) != 0 ? 2 : 4);

    public byte StringHeapIndexSize => (byte)((_heapSizes & HeapSizes.StringHeapSmall) != 0 ? 2 : 4);

    public const int MaxRowCount = 0xFF_FFFF; // 2^24 - 1

    public const int MaxSymbolRowCount = 0xF_FFFF; // 2^20 - 1

    private static byte GetIndexSize(int rowCount) => rowCount switch
    {
        < 0xFF => 1,
        < 0xFFFF => 2,
        _ => 4
    };

    private static byte GetBinaryCodedIndexSize(int row1Count, int row2Count) => (row1Count | row2Count) switch
    {
        < 0x7F => 1,
        < 0x7FF => 2,
        _ => 4
    };

    private StringHandle ReadStringHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(index, StringHeapIndexSize));

    private BlobHandle ReadBlobHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(index, BlobHeapIndexSize));

    public GrammarTables(ReadOnlySpan<byte> grammarFile, int tableStreamOffset, int tableStreamLength, out bool hasUnknownTables) : this()
    {
        if (tableStreamLength < sizeof(ulong))
        {
            ThrowHelpers.ThrowInvalidDataException("Too small table stream header.");
        }

        TableKinds presentTables = (TableKinds)grammarFile.ReadUInt64(tableStreamOffset);
        hasUnknownTables = (presentTables & ~TableKinds.All) != 0;
        if ((presentTables & TableKinds.Grammar) == 0)
        {
            ThrowHelpers.ThrowInvalidDataException("Grammar table is missing.");
        }

        int tableCount = BitOperationsCompat.PopCount((ulong)presentTables);
        int tableHeaderSizeUnaligned =
            // TablesPresent
            sizeof(ulong)
            // RowCounts and RowSizes, one for each table present
            + tableCount * (sizeof(int) + sizeof(byte))
            // HeapSizes
            + sizeof(byte);
        int tableHeaderSize = BitArithmetic.Align(tableHeaderSizeUnaligned, sizeof(ulong));
        if (tableStreamLength < tableHeaderSize)
        {
            ThrowHelpers.ThrowInvalidDataException("Table boundaries are missing.");
        }

        int rowCountsBase = tableStreamOffset + sizeof(ulong);
        int rowSizesBase = rowCountsBase + tableCount * sizeof(int);
        int currentTableBase = tableStreamOffset + tableHeaderSize;
        ulong remainingTables = (ulong)presentTables;
        int i = 0;

        int grammarBase = currentTableBase;
        int tokenSymbolBase = 0;
        int groupBase = 0;
        int groupNestingBase = 0;
        int nonterminalBase = 0;
        int productionBase = 0;
        int productionMemberBase = 0;
        int stateMachineBase = 0;
        int specialNameBase = 0;

        byte grammarTableRowSize = 0;

        _heapSizes = (HeapSizes)grammarFile[tableStreamOffset + tableHeaderSizeUnaligned - 1];
        while (remainingTables != 0)
        {
            int currentTable = BitOperationsCompat.TrailingZeroCount(remainingTables);

            int rowCount = grammarFile.ReadInt32(rowCountsBase + i * sizeof(int));
            int rowLimit = ((TableKind)currentTable) switch
            {
                TableKind.Grammar => 1,
                TableKind.TokenSymbol or TableKind.Nonterminal => MaxSymbolRowCount,
                _ => MaxRowCount
            };
            if ((uint)rowCount > rowLimit)
            {
                ThrowHelpers.ThrowInvalidDataException("Table has too many rows.");
            }
            byte rowSize = grammarFile[rowSizesBase + i];
            switch ((TableKind)currentTable)
            {
                case TableKind.Grammar:
                    grammarTableRowSize = rowSize;
                    break;
                case TableKind.TokenSymbol:
                    tokenSymbolBase = currentTableBase;
                    TokenSymbolRowCount = rowCount;
                    TokenSymbolRowSize = rowSize;
                    break;
                case TableKind.Group:
                    groupBase = currentTableBase;
                    GroupRowCount = rowCount;
                    GroupRowSize = rowSize;
                    break;
                case TableKind.GroupNesting:
                    groupNestingBase = currentTableBase;
                    GroupNestingRowCount = rowCount;
                    GroupNestingRowSize = rowSize;
                    break;
                case TableKind.Nonterminal:
                    nonterminalBase = currentTableBase;
                    NonterminalRowCount = rowCount;
                    NonterminalRowSize = rowSize;
                    break;
                case TableKind.Production:
                    productionBase = currentTableBase;
                    ProductionRowCount = rowCount;
                    ProductionRowSize = rowSize;
                    break;
                case TableKind.ProductionMember:
                    productionMemberBase = currentTableBase;
                    ProductionMemberRowCount = rowCount;
                    ProductionMemberRowSize = rowSize;
                    break;
                case TableKind.StateMachine:
                    stateMachineBase = currentTableBase;
                    StateMachineRowCount = rowCount;
                    StateMachineRowSize = rowSize;
                    break;
                case TableKind.SpecialName:
                    specialNameBase = currentTableBase;
                    SpecialNameRowCount = rowCount;
                    SpecialNameRowSize = rowSize;
                    break;
            }
            currentTableBase += rowCount * rowSize;

            remainingTables &= remainingTables - 1;
            i++;
        }

        Debug.Assert(i == tableCount - 1);
        if (tableStreamLength != currentTableBase)
        {
            ThrowHelpers.ThrowInvalidDataException("Too small table stream.");
        }

        int tokenSymbolIndexSize = GetIndexSize(TokenSymbolRowCount);
        int groupNestingIndexSize = GetIndexSize(GroupNestingRowCount);
        int nonterminalIndexSize = GetIndexSize(NonterminalRowCount);
        int productionIndexSize = GetIndexSize(ProductionRowCount);
        int productionMemberIndexSize = GetIndexSize(ProductionMemberRowCount);

        int productionMemberCodedIndexSize = GetBinaryCodedIndexSize(TokenSymbolRowCount, NonterminalRowCount);

        ValidateRowCount(TableKind.Grammar, grammarTableRowSize,
            StringHeapIndexSize + nonterminalIndexSize + sizeof(ushort));
        GrammarNameOffset = grammarBase + 0;
        GrammarStartSymbolOffset = GrammarNameOffset + StringHeapIndexSize;
        GrammarFlagsOffset = GrammarStartSymbolOffset + nonterminalIndexSize;

        ValidateRowCount(TableKind.TokenSymbol, TokenSymbolRowSize,
            StringHeapIndexSize + sizeof(ushort));
        TokenSymbolNameBase = tokenSymbolBase + 0;
        TokenSymbolFlagsBase = TokenSymbolNameBase + StringHeapIndexSize;

        ValidateRowCount(TableKind.Group, GroupRowSize,
            StringHeapIndexSize + tokenSymbolIndexSize + sizeof(ushort) + tokenSymbolIndexSize + tokenSymbolIndexSize + groupNestingIndexSize);
        GroupNameBase = groupBase + 0;
        GroupContainerBase = GroupNameBase + StringHeapIndexSize;
        GroupFlagsBase = GroupContainerBase + tokenSymbolIndexSize;
        GroupStartBase = GroupFlagsBase + sizeof(ushort);
        GroupEndBase = GroupStartBase + tokenSymbolIndexSize;
        GroupFirstNestingBase = GroupEndBase + tokenSymbolIndexSize;

        ValidateRowCount(TableKind.GroupNesting, GroupNestingRowSize,
            groupNestingIndexSize);
        GroupNestingGroupBase = groupNestingBase + 0;

        ValidateRowCount(TableKind.Nonterminal, NonterminalRowSize,
            StringHeapIndexSize + sizeof(ushort) + productionIndexSize);
        NonterminalNameBase = nonterminalBase + 0;
        NonterminalFlagsBase = NonterminalNameBase + StringHeapIndexSize;
        NonterminalFirstProductionBase = NonterminalFlagsBase + sizeof(ushort);

        ValidateRowCount(TableKind.Production, ProductionRowSize,
            productionMemberIndexSize);
        ProductionFirstMemberBase = productionBase + 0;

        ValidateRowCount(TableKind.ProductionMember, ProductionMemberRowSize,
            productionMemberCodedIndexSize);
        ProductionMemberMemberBase = productionMemberBase + 0;

        ValidateRowCount(TableKind.StateMachine, StateMachineRowSize,
            sizeof(ulong) + BlobHeapIndexSize);
        StateMachineKindBase = stateMachineBase + 0;
        StateMachineDataBase = StateMachineKindBase + sizeof(ulong);

        ValidateRowCount(TableKind.SpecialName, SpecialNameRowSize,
            StringHeapIndexSize + productionMemberCodedIndexSize);
        SpecialNameNameBase = specialNameBase + 0;
        SpecialNameSymbolBase = SpecialNameNameBase + StringHeapIndexSize;
    }

    private static void ValidateRowCount(TableKind table, byte actual, int expected)
    {
        if (actual != expected)
        {
            Throw(table);
        }

        [DoesNotReturn, StackTraceHidden]
        static void Throw(TableKind table) =>
            ThrowHelpers.ThrowInvalidDataException($"Invalid row size for {table} table.");
    }

    [Flags]
    private enum HeapSizes : byte
    {
        StringHeapSmall = 1,
        BlobHeapSmall = 2
    }
}
