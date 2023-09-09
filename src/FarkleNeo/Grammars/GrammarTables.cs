// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static Farkle.Grammars.GrammarUtilities;

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
    public readonly byte ProductionRowSize;
    public readonly int ProductionHeadBase, ProductionFirstMemberBase;

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

    public readonly int TerminalCount;

    private readonly GrammarHeapSizes _heapSizes;

    public byte BlobHeapIndexSize => (byte)((_heapSizes & GrammarHeapSizes.BlobHeapSmall) != 0 ? 2 : 4);

    public byte StringHeapIndexSize => (byte)((_heapSizes & GrammarHeapSizes.StringHeapSmall) != 0 ? 2 : 4);

    public const int MaxRowCount = 0xFF_FFFF; // 2^24 - 1

    public const int MaxSymbolRowCount = 0xF_FFFF; // 2^20 - 1

    private static int GetTableCellOffset(int columnBase, int rowCount, byte rowSize, uint index)
    {
        Debug.Assert(index != 0);

        if (index > (uint)rowCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(null);
        }

        return columnBase + rowSize * ((int)index - 1);
    }

    private static uint ReadTableIndex(ReadOnlySpan<byte> grammarFile, int index, int rowCount) =>
        grammarFile.ReadUIntVariableSize(index, GetCompressedIndexSize(rowCount));

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

    private int CountTerminalsFast(ReadOnlySpan<byte> grammarFile)
    {
        // Because usually almost all token symbols are terminals, we can count them
        // by looping from the end and stopping at the first terminal we find. This
        // would cause problems if the terminals are at the beginning of the table
        // but that's the job of content validation to detect.
        for (int i = TokenSymbolRowCount; i > 0; i--)
        {
            TokenSymbolAttributes flags = GetTokenSymbolFlags(grammarFile, (uint)i);
            if ((flags & TokenSymbolAttributes.Terminal) != 0)
            {
                return i;
            }
        }
        return 0;
    }

    public bool IsTerminal(TokenSymbolHandle handle) => handle.HasValue && handle.Value < TerminalCount;

    public GrammarTables(ReadOnlySpan<byte> grammarFile, GrammarFileSection section, out bool hasUnknownTables) : this()
    {
        if (section.Length < sizeof(ulong))
        {
            ThrowHelpers.ThrowInvalidDataException("Too small table stream header.");
        }

        TableKinds presentTables = (TableKinds)grammarFile.ReadUInt64(section.Offset);
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
        if (section.Length < tableHeaderSize)
        {
            ThrowHelpers.ThrowInvalidDataException("Table boundaries are missing.");
        }

        int rowCountsBase = section.Offset + sizeof(ulong);
        int rowSizesBase = rowCountsBase + tableCount * sizeof(int);
        int currentTableBase = section.Offset + tableHeaderSize;
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

        _heapSizes = (GrammarHeapSizes)grammarFile[section.Offset + tableHeaderSizeUnaligned - 1];
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
            byte rowSize = grammarFile[rowSizesBase + i];
            if ((uint)rowCount > rowLimit)
            {
                ThrowHelpers.ThrowInvalidDataException("Table has too many rows.");
            }
            if (rowCount == 0)
            {
                ThrowHelpers.ThrowInvalidDataException("Table row count cannot be zero.");
            }
            if (rowSize == 0)
            {
                ThrowHelpers.ThrowInvalidDataException("Table row size cannot be zero.");
            }
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

        Debug.Assert(i == tableCount);
        if (currentTableBase != section.Offset + section.Length)
        {
            ThrowHelpers.ThrowInvalidDataException("Incorrect table stream size.");
        }

        int tokenSymbolIndexSize = GetCompressedIndexSize(TokenSymbolRowCount);
        int groupNestingIndexSize = GetCompressedIndexSize(GroupNestingRowCount);
        int nonterminalIndexSize = GetCompressedIndexSize(NonterminalRowCount);
        int productionIndexSize = GetCompressedIndexSize(ProductionRowCount);
        int productionMemberIndexSize = GetCompressedIndexSize(ProductionMemberRowCount);

        int productionMemberCodedIndexSize = GetBinaryCodedIndexSize(TokenSymbolRowCount, NonterminalRowCount);

        ValidateRowCount(TableKind.Grammar, grammarTableRowSize,
            StringHeapIndexSize + nonterminalIndexSize + sizeof(ushort));
        GrammarNameOffset = grammarBase + 0;
        GrammarStartSymbolOffset = GrammarNameOffset + StringHeapIndexSize;
        GrammarFlagsOffset = GrammarStartSymbolOffset + nonterminalIndexSize;

        ValidateRowCount(TableKind.TokenSymbol, TokenSymbolRowSize,
            StringHeapIndexSize + sizeof(uint));
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
            nonterminalIndexSize + productionMemberIndexSize);
        ProductionHeadBase = productionBase + 0;
        ProductionFirstMemberBase = ProductionHeadBase + nonterminalIndexSize;

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

        TerminalCount = CountTerminalsFast(grammarFile);
    }

    public StringHandle GetGrammarName(ReadOnlySpan<byte> grammarFile) =>
        ReadStringHandle(grammarFile, GrammarNameOffset);

    public NonterminalHandle GetGrammarStartSymbol(ReadOnlySpan<byte> grammarFile) =>
        ReadNonterminalHandle(grammarFile, GrammarStartSymbolOffset);

    public GrammarAttributes GetGrammarFlags(ReadOnlySpan<byte> grammarFile) =>
        (GrammarAttributes)grammarFile.ReadUInt16(GrammarFlagsOffset);

    public StringHandle GetTokenSymbolName(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(TokenSymbolNameBase, TokenSymbolRowCount, TokenSymbolRowSize, index));

    public TokenSymbolAttributes GetTokenSymbolFlags(ReadOnlySpan<byte> grammarFile, uint index) =>
        (TokenSymbolAttributes)grammarFile.ReadUInt32(GetTableCellOffset(TokenSymbolFlagsBase, TokenSymbolRowCount, TokenSymbolRowSize, index));

    public uint GetTokenSymbolStartedGroup(ReadOnlySpan<byte> grammarFile, uint index)
    {
        for (int i = 1; i <= GroupRowCount; i++)
        {
            var groupStart = GetGroupStart(grammarFile, (uint)i).TableIndex;
            if (groupStart == index)
            {
                return (uint)i;
            }
        }

        return 0;
    }

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

    public (uint Offset, uint NextOffset) GetGroupNestingBounds(ReadOnlySpan<byte> grammarFile, uint index)
    {
        uint firstNesting = GetGroupFirstNesting(grammarFile, index);
        uint firstNestingOfNext = index < (uint)GroupRowCount ? GetGroupFirstNesting(grammarFile, index + 1) : (uint)GroupNestingRowCount + 1;
        Debug.Assert(firstNesting <= firstNestingOfNext);
        return (firstNesting, firstNestingOfNext);
    }

    public bool CanGroupNest(ReadOnlySpan<byte> grammarFile, uint outerIndex, uint innerIndex)
    {
        (uint offset, uint nextOffset) = GetGroupNestingBounds(grammarFile, outerIndex);
        for (uint i = offset; i < nextOffset; i++)
        {
            uint nesting = GetGroupNestingGroup(grammarFile, i);
            if (nesting == innerIndex)
            {
                return true;
            }
        }
        return false;
    }

    public StringHandle GetNonterminalName(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadStringHandle(grammarFile, GetTableCellOffset(NonterminalNameBase, NonterminalRowCount, NonterminalRowSize, index));

    public NonterminalAttributes GetNonterminalFlags(ReadOnlySpan<byte> grammarFile, uint index) =>
        (NonterminalAttributes)grammarFile.ReadUInt16(GetTableCellOffset(NonterminalFlagsBase, NonterminalRowCount, NonterminalRowSize, index));

    public ProductionHandle GetNonterminalFirstProduction(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadProductionHandle(grammarFile, GetTableCellOffset(NonterminalFirstProductionBase, NonterminalRowCount, NonterminalRowSize, index));

    public NonterminalHandle GetProductionHead(ReadOnlySpan<byte> grammarFile, uint index) =>
        ReadNonterminalHandle(grammarFile, GetTableCellOffset(ProductionHeadBase, ProductionRowCount, ProductionRowSize, index));

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

    public void ValidateContent(ReadOnlySpan<byte> grammarFile, in StringHeap stringHeap, in BlobHeap blobHeap)
    {
        HashSet<uint>? groupStarts = null;

        {
            _ = stringHeap.GetStringSection(grammarFile, GetGrammarName(grammarFile));
            ValidateHandle(GetGrammarStartSymbol(grammarFile));
        }

        bool rejectTerminals = false;
        for (uint i = 1; i <= (uint)TokenSymbolRowCount; i++)
        {
            _ = stringHeap.GetStringSection(grammarFile, GetTokenSymbolName(grammarFile, i));
            TokenSymbolAttributes flags = GetTokenSymbolFlags(grammarFile, i);
            bool isTerminal = (flags & TokenSymbolAttributes.Terminal) != 0;
            bool isGroupStart = (flags & TokenSymbolAttributes.GroupStart) != 0;
            if (isTerminal)
            {
                Assert(!rejectTerminals, "Terminals must come before other token symbols.");
                rejectTerminals = false;
            }
            if (isGroupStart)
            {
                Assert(!isTerminal, "Terminals must not have the GroupStart flag set.");
                bool added = (groupStarts ??= new()).Add(i);
                Debug.Assert(added);
            }
        }

        if (GroupRowCount != 0)
        {
            Assert(groupStarts is not null);
            uint previousFirstNesting = GetGroupFirstNesting(grammarFile, 1);
            Assert(previousFirstNesting == 1);
            for (uint i = 1; i <= (uint)GroupRowCount; i++)
            {
                _ = stringHeap.GetStringSection(grammarFile, GetGroupName(grammarFile, i));
                TokenSymbolHandle container = GetGroupContainer(grammarFile, i);
                ValidateHandle(container);
                Assert((GetTokenSymbolFlags(grammarFile, container.TableIndex) & TokenSymbolAttributes.GroupStart) == 0, "Group container must not have the GroupStart flag set.");
                TokenSymbolHandle start = GetGroupStart(grammarFile, i);
                ValidateHandle(start);
                Assert((GetTokenSymbolFlags(grammarFile, start.TableIndex) & TokenSymbolAttributes.GroupStart) != 0, "Group start must have the GroupStart flag set.");
                Assert(groupStarts.Remove(start.TableIndex), "Group start must be a group start symbol and start only one group.");
                TokenSymbolHandle end = GetGroupEnd(grammarFile, i);
                ValidateHandle(end);
                Assert((GetTokenSymbolFlags(grammarFile, end.TableIndex) & TokenSymbolAttributes.GroupStart) == 0, "Group end must not have the GroupStart flag set.");

                uint firstNesting = GetGroupFirstNesting(grammarFile, i);
                Assert(firstNesting >= previousFirstNesting, "Group first nestings are out of sequence.");
                previousFirstNesting = firstNesting;
                // The First*** columns can be one number bigger than their respective row count.
                // This can happen if the row and all its subsequent ones have no child items.
                ValidateHandle(firstNesting, GroupNestingRowCount + 1);
            }
            Assert(groupStarts.Count == 0, "All token symbols with the GroupStart flag set must start a group.");
        }

        if (GroupNestingRowCount != 0)
        {
            Assert(GroupRowCount != 0);
        }
        for (uint i = 1; i <= (uint)GroupNestingRowCount; i++)
        {
            ValidateHandle(GetGroupNestingGroup(grammarFile, i), GroupRowCount);
        }

        if (NonterminalRowCount != 0)
        {
            ProductionHandle previousFirstProduction = GetNonterminalFirstProduction(grammarFile, 1);
            Assert(previousFirstProduction.TableIndex == 1);
            for (uint i = 1; i <= (uint)NonterminalRowCount; i++)
            {
                _ = stringHeap.GetStringSection(grammarFile, GetNonterminalName(grammarFile, i));

                ProductionHandle firstProduction = GetNonterminalFirstProduction(grammarFile, i);
                Assert(firstProduction.TableIndex >= previousFirstProduction.TableIndex, "Nonterminal first productions are out of sequence.");
                previousFirstProduction = firstProduction;
                ValidateHandle(firstProduction.TableIndex, ProductionRowCount + 1);
            }
        }

        if (ProductionRowCount != 0)
        {
            Assert(NonterminalRowCount != 0);
            uint previousFirstMember = GetProductionFirstMember(grammarFile, 1);
            uint currentHead = 1;
            Assert(previousFirstMember == 1);
            for (uint i = 1; i <= (uint)ProductionRowCount; i++)
            {
                while (currentHead < NonterminalRowCount && GetNonterminalFirstProduction(grammarFile, currentHead + 1).TableIndex <= i)
                {
                    currentHead++;
                }
                Assert(GetProductionHead(grammarFile, i).TableIndex == currentHead, "Invalid production head");

                uint firstMember = GetProductionFirstMember(grammarFile, i);
                Assert(firstMember >= previousFirstMember, "Production first members are out of sequence.");
                previousFirstMember = firstMember;
                ValidateHandle(firstMember, ProductionMemberRowCount + 1);
            }
            Assert(currentHead == NonterminalRowCount);
        }

        for (uint i = 1; i <= (uint)ProductionMemberRowCount; i++)
        {
            EntityHandle member = GetProductionMemberMember(grammarFile, i);
            Assert(member.HasValue);
            if (member.IsTokenSymbol)
            {
                Assert(IsTerminal((TokenSymbolHandle)member), "Token symbols in productions must have the Terminal flag set.");
                ValidateHandle((TokenSymbolHandle)member);
            }
            else
            {
                ValidateHandle((NonterminalHandle)member);
            }
        }

        for (uint i = 1; i <= (uint)StateMachineRowCount; i++)
        {
            _ = blobHeap.GetBlobSection(grammarFile, GetStateMachineData(grammarFile, i));
        }

        for (uint i = 1; i <= (uint)SpecialNameRowCount; i++)
        {
            _ = stringHeap.GetStringSection(grammarFile, GetSpecialNameName(grammarFile, i));
            EntityHandle member = GetSpecialNameSymbol(grammarFile, i);
            Assert(member.HasValue);
            if (member.IsTokenSymbol)
            {
                ValidateHandle((TokenSymbolHandle)member);
            }
            else
            {
                ValidateHandle((NonterminalHandle)member);
            }
        }

        static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                ThrowHelpers.ThrowInvalidDataException(message);
            }
        }
    }

    internal void ValidateHandle(TokenSymbolHandle handle) => ValidateHandle(handle.TableIndex, TokenSymbolRowCount);

    internal void ValidateHandle(NonterminalHandle handle) => ValidateHandle(handle.TableIndex, NonterminalRowCount);

    internal void ValidateHandle(ProductionHandle handle) => ValidateHandle(handle.TableIndex, ProductionRowCount);

    private static void ValidateHandle(uint tableIndex, int rowCount)
    {
        if (tableIndex > (uint)rowCount)
        {
            ThrowHelpers.ThrowInvalidDataException("Invalid handle.");
        }
    }

    private static void ValidateRowCount(TableKind table, byte actual, int expected)
    {
        // A value of zero indicates that the table was not present.
        if (actual != 0 && actual != expected)
        {
            Throw(table);
        }

        [DoesNotReturn, StackTraceHidden]
        static void Throw(TableKind table) =>
            ThrowHelpers.ThrowInvalidDataException($"Invalid row size for {table} table.");
    }
}
