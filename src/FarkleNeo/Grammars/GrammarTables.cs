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

    private readonly GrammarHeapSizes _heapSizes;

    public byte BlobHeapIndexSize => (byte)((_heapSizes & GrammarHeapSizes.BlobHeapSmall) != 0 ? 2 : 4);

    public byte StringHeapIndexSize => (byte)((_heapSizes & GrammarHeapSizes.StringHeapSmall) != 0 ? 2 : 4);

    public const int MaxRowCount = 0xFF_FFFF; // 2^24 - 1

    public const int MaxSymbolRowCount = 0xF_FFFF; // 2^20 - 1

    public static byte GetIndexSize(int rowCount) => rowCount switch
    {
        < 0xFF => 1,
        < 0xFFFF => 2,
        _ => 4
    };

    public static byte GetBinaryCodedIndexSize(int row1Count, int row2Count) => (row1Count | row2Count) switch
    {
        < 0x7F => 1,
        < 0x7FF => 2,
        _ => 4
    };

    private static int GetTableCellOffset(int columnBase, int rowCount, byte rowSize, uint index)
    {
        // Remember, indices are one-based.
        if (index == 0)
        {
            ThrowHelpers.ThrowArgumentNullException(null);
        }

        if (index > (uint)rowCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(null);
        }

        return columnBase + rowSize * ((int)index - 1);
    }

    private static uint ReadTableIndex(ReadOnlySpan<byte> grammarFile, int index, int rowCount) =>
        grammarFile.ReadUIntVariableSize(index, GetIndexSize(rowCount));

    private StringHandle ReadStringHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(index, StringHeapIndexSize));

    private BlobHandle ReadBlobHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadUIntVariableSize(index, BlobHeapIndexSize));

    private TokenSymbolHandle ReadTokenSymbolHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(ReadTableIndex(grammarFile, index, TokenSymbolRowCount));

    private NonterminalHandle ReadNonterminalHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(ReadTableIndex(grammarFile, index, NonterminalRowCount));

    private ProductionHandle ReadProductionHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        new(ReadTableIndex(grammarFile, index, ProductionRowCount));

    // These table kinds won't be exposed to users and don't need their own handle type.
    private uint ReadGroupHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        ReadTableIndex(grammarFile, index, GroupRowCount);

    private uint ReadGroupNestingHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        ReadTableIndex(grammarFile, index, GroupNestingRowCount);

    private uint ReadProductionMemberHandle(ReadOnlySpan<byte> grammarFile, int index) =>
        ReadTableIndex(grammarFile, index, ProductionMemberRowCount);

    private EntityHandle ReadSymbolHandle(ReadOnlySpan<byte> grammarFile, int index)
    {
        byte indexSize = GetBinaryCodedIndexSize(TokenSymbolRowCount, NonterminalRowCount);
        uint codedIndex = grammarFile.ReadUIntVariableSize(index, indexSize);

        // TableKind is byte-sized so the compiler optimizes away the array allocation on all frameworks.
        ReadOnlySpan<TableKind> tableKinds = new TableKind[] { TableKind.TokenSymbol, TableKind.Nonterminal };
        TableKind kind = tableKinds[(int)codedIndex & 1];

        uint indexValue = codedIndex >> 1;
        return new(indexValue, kind);
    }

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

        _heapSizes = (GrammarHeapSizes)grammarFile[tableStreamOffset + tableHeaderSizeUnaligned - 1];
        while (remainingTables != 0)
        {
            int currentTable = BitOperationsCompat.TrailingZeroCount(remainingTables);

            int rowCount = grammarFile.ReadInt32(rowCountsBase + i * sizeof(int));
            int rowLimit = (TableKind)currentTable switch
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

    public StringHandle GetGrammarName(ReadOnlySpan<byte> grammarFile) =>
        ReadStringHandle(grammarFile, GrammarNameOffset);

    public NonterminalHandle GetGrammarStartSymbol(ReadOnlySpan<byte> grammarFile) =>
        ReadNonterminalHandle(grammarFile, GrammarStartSymbolOffset);

    public GrammarAttributes GetGrammarFlags(ReadOnlySpan<byte> grammarFile) =>
        (GrammarAttributes)grammarFile.ReadUInt16(GrammarFlagsOffset);

    public StringHandle GetTokenSymbolName(ReadOnlySpan<byte> grammarFile, TokenSymbolHandle index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(TokenSymbolNameBase, TokenSymbolRowCount, TokenSymbolRowSize, index.TableIndex));

    public TokenSymbolAttributes GetTokenSymbolFlags(ReadOnlySpan<byte> grammarFile, TokenSymbolHandle index) =>
        (TokenSymbolAttributes)grammarFile.ReadUInt32(GetTableCellOffset(TokenSymbolFlagsBase, TokenSymbolRowCount, TokenSymbolRowSize, index.TableIndex));

    public StringHandle GetGroupName(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(GroupNameBase, GroupRowCount, GroupRowSize, index));

    public TokenSymbolHandle GetGroupContainer(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadTokenSymbolHandle(grammarFile, GetTableCellOffset(GroupContainerBase, GroupRowCount, GroupRowSize, index));

    public GroupAttributes GetGroupFlags(ReadOnlySpan<byte> grammarFile, uint index) =>
        (GroupAttributes)grammarFile.ReadUInt16(GetTableCellOffset(GroupFlagsBase, GroupRowCount, GroupRowSize, index));

    public TokenSymbolHandle GetGroupStart(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadTokenSymbolHandle(grammarFile, GetTableCellOffset(GroupStartBase, GroupRowCount, GroupRowSize, index));

    public TokenSymbolHandle GetGroupEnd(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadTokenSymbolHandle(grammarFile, GetTableCellOffset(GroupEndBase, GroupRowCount, GroupRowSize, index));

    public uint GetGroupFirstNesting(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadGroupNestingHandle(grammarFile, GetTableCellOffset(GroupFirstNestingBase, GroupRowCount, GroupRowSize, index));

    public uint GetGroupNestingGroup(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadGroupHandle(grammarFile, GetTableCellOffset(GroupNestingGroupBase, GroupNestingRowCount, GroupNestingRowSize, index));

    public StringHandle GetNonterminalName(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(NonterminalNameBase, NonterminalRowCount, NonterminalRowSize, index));

    public NonterminalAttributes GetNonterminalFlags(ReadOnlySpan<byte> grammarFile, uint index) =>
        (NonterminalAttributes)grammarFile.ReadUInt16(GetTableCellOffset(NonterminalFlagsBase, NonterminalRowCount, NonterminalRowSize, index));

    public ProductionHandle GetNonterminalFirstProduction(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadProductionHandle(grammarFile, GetTableCellOffset(NonterminalFirstProductionBase, NonterminalRowCount, NonterminalRowSize, index));

    public uint GetProductionFirstMember(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadProductionMemberHandle(grammarFile, GetTableCellOffset(ProductionFirstMemberBase, ProductionRowCount, ProductionRowSize, index));

    public EntityHandle GetProductionMemberMember(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadSymbolHandle(grammarFile, GetTableCellOffset(ProductionMemberMemberBase, ProductionMemberRowCount, ProductionMemberRowSize, index));

    public ulong GetStateMachineKind(ReadOnlySpan<byte> grammarFile, uint index) =>
        grammarFile.ReadUInt64(GetTableCellOffset(StateMachineKindBase, StateMachineRowCount, StateMachineRowSize, index));

    public BlobHandle GetStateMachineData(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadBlobHandle(grammarFile, GetTableCellOffset(StateMachineDataBase, StateMachineRowCount, StateMachineRowSize, index));

    public StringHandle GetSpecialNameName(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(SpecialNameNameBase, SpecialNameRowCount, SpecialNameRowSize, index));

    public EntityHandle GetSpecialNameSymbol(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadSymbolHandle(grammarFile, GetTableCellOffset(SpecialNameSymbolBase, SpecialNameRowCount, SpecialNameRowSize, index));

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
}
