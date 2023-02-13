// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars;

internal struct GrammarTablesBuilder
{
    private StringHandle _grammarName;
    private NonterminalHandle _grammarStartSymbol;
    private GrammarAttributes _grammarFlags;
    private bool _isGrammarRowSet;

    private List<TokenSymbolRow>? _tokenSymbols;
    private bool _rejectTerminals;

    private List<GroupRow>? _groups;

    private List<uint>? _groupNestingGroups;
    private int _requiredGroupNestings;

    private List<NonterminalRow>? _nonterminals;

    private List<int>? _productionMemberCounts;
    private int _requiredProductions;

    private List<EntityHandle>? _productionMemberMembers;
    private int _requiredProductionMembers;

    private HashSet<ulong>? _presentStateMachines;
    private List<StateMachineRow>? _stateMachines;

    private HashSet<StringHandle>? _presentSpecialNames;
    private List<SpecialNameRow>? _specialNames;

    private static uint EncodeSymbolCodedIndex(EntityHandle handle)
    {
        if (handle.IsTokenSymbol)
        {
            return (handle.Value << 1) | 0;
        }
        else
        {
            Debug.Assert(handle.IsNonterminal);
            return (handle.Value << 1) | 1;
        }
    }

    private static void ValidateHandle<T>(uint handle, [NotNull] List<T>? list, string parameterName)
    {
        if (list is null || handle > (uint)list.Count)
        {
            ThrowHelpers.ThrowArgumentException(parameterName, "Invalid handle.");
        }
    }

    private static void ValidateRequiredRowCount<T>(List<T>? list, int requiredCount, string message)
    {
        int count = list is null ? 0 : list.Count;
        if (count != requiredCount)
        {
            ThrowHelpers.ThrowInvalidOperationException(message);
        }
    }

    private static void ValidateRowCount<T>(List<T>? list, TableKind tableKind, int maxCount = GrammarTables.MaxRowCount)
    {
        if (list is { Count: int count } && count >= maxCount)
        {
            Throw(tableKind);
        }

        [DoesNotReturn, StackTraceHidden]
        static void Throw(TableKind tableKind) =>
            ThrowHelpers.ThrowInvalidOperationException($"{tableKind} table's row limit has been reached.");
    }

    public void SetGrammarInfo(StringHandle name, NonterminalHandle startSymbol, GrammarAttributes flags)
    {
        if (_isGrammarRowSet)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot set grammar info more than once.");
        }

        ValidateHandle(startSymbol.Value, _nonterminals, nameof(startSymbol));

        _grammarName = name;
        _grammarStartSymbol = startSymbol;
        _grammarFlags = flags;
        _isGrammarRowSet = true;
    }

    public TokenSymbolHandle AddTokenSymbol(StringHandle name, TokenSymbolAttributes flags)
    {
        ValidateRowCount(_tokenSymbols, TableKind.TokenSymbol, GrammarTables.MaxSymbolRowCount);
        if ((flags & TokenSymbolAttributes.Terminal) != 0)
        {
            if (_rejectTerminals)
            {
                ThrowHelpers.ThrowInvalidOperationException("Cannot add a terminal after a non-terminal token symbol has been added.");
            }

            if ((flags & TokenSymbolAttributes.GroupStart) != 0)
            {
                ThrowHelpers.ThrowArgumentException(nameof(flags), "A terminal cannot start a group.");
            }
        }
        else
        {
            _rejectTerminals = true;
        }

        var tokenSymbols = _tokenSymbols ??= new();
        tokenSymbols.Add(new() { Name = name, Flags = flags });
        return new((uint)tokenSymbols.Count);
    }

    public uint AddGroup(StringHandle name, TokenSymbolHandle container, GroupAttributes flags, TokenSymbolHandle start, TokenSymbolHandle end, int nestingCount)
    {
        ValidateRowCount(_groups, TableKind.Group);
        ValidateHandle(container.Value, _groups, nameof(container));
        ValidateHandle(start.Value, _groups, nameof(start));
        ValidateHandle(end.Value, _groups, nameof(end));
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(nestingCount);

        var groups = _groups ??= new();
        groups.Add(new() { Name = name, Container = container, Flags = flags, Start = start, End = end, NestingCount = nestingCount });
        if (nestingCount > 0)
        {
            _groupNestingGroups ??= new();
        }
        _requiredGroupNestings += nestingCount;
        return (uint)groups.Count;
    }

    public void AddGroupNesting(uint group)
    {
        ValidateRowCount(_groupNestingGroups, TableKind.GroupNesting);
        ValidateHandle(group, _groups, nameof(group));

        var groupNestingGroups = _groupNestingGroups;
        if (groupNestingGroups is not { Count: int count } || count >= _requiredGroupNestings)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot add more group nestings, please add a group first.");
        }

        groupNestingGroups.Add(group);
    }

    public NonterminalHandle AddNonterminal(StringHandle name, NonterminalAttributes flags, int productionCount)
    {
        ValidateRowCount(_nonterminals, TableKind.Nonterminal, GrammarTables.MaxSymbolRowCount);
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(productionCount);

        var nonterminals = _nonterminals ??= new();
        nonterminals.Add(new() { Name = name, Flags = flags, ProductionCount = productionCount });
        if (productionCount > 0)
        {
            _productionMemberCounts ??= new();
        }
        _requiredProductions += productionCount;
        return new((uint)nonterminals.Count);
    }

    public ProductionHandle AddProduction(int memberCount)
    {
        ValidateRowCount(_productionMemberCounts, TableKind.Production);
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(memberCount);

        var productionMemberCounts = _productionMemberCounts;
        if (productionMemberCounts is not { Count: int count } || count >= _requiredProductions)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot add more productions; please add a nonterminal first.");
        }

        productionMemberCounts.Add(memberCount);
        if (memberCount > 0)
        {
            _productionMemberMembers ??= new();
        }
        _requiredProductionMembers += memberCount;
        return new((uint)productionMemberCounts.Count);
    }

    public void AddProductionMember(EntityHandle member)
    {
        ValidateRowCount(_productionMemberMembers, TableKind.ProductionMember);
        if (member.IsTokenSymbol)
        {
            var tokenSymbol = (TokenSymbolHandle)member;
            ValidateHandle(tokenSymbol.Value, _tokenSymbols, nameof(member));
            if ((_tokenSymbols[(int)tokenSymbol.Value - 1].Flags & TokenSymbolAttributes.Terminal) == 0)
            {
                ThrowHelpers.ThrowArgumentException(nameof(member), "Token symbols must have the Terminal flag set.");
            }
        }
        else if (member.IsNonterminal)
        {
            var nonterminal = (NonterminalHandle)member;
            ValidateHandle(nonterminal.Value, _nonterminals, nameof(member));
        }
        else
        {
            ThrowHelpers.ThrowArgumentException(nameof(member), "A production member must be a token symbol or nonterminal.");
        }

        var productionMemberCounts = _productionMemberMembers;
        if (productionMemberCounts is not { Count: int count } || count >= _requiredProductionMembers)
        {
            ThrowHelpers.ThrowInvalidOperationException("Cannot add more production members; please add a production first.");
        }

        productionMemberCounts.Add(member);
    }

    public void AddStateMachine(ulong kind, BlobHandle data)
    {
        ValidateRowCount(_stateMachines, TableKind.StateMachine);

        if (!(_presentStateMachines ??= new()).Add(kind))
        {
            ThrowHelpers.ThrowArgumentException(nameof(kind), "Cannot add the same kind of state machine multiple times.");
        }

        (_stateMachines ??= new()).Add(new() { Kind = kind, Data = data });
    }

    public void AddSpecialName(StringHandle name, EntityHandle symbol)
    {
        ValidateRowCount(_specialNames, TableKind.SpecialName);
        if (symbol.IsTokenSymbol)
        {
            var tokenSymbol = (TokenSymbolHandle)symbol;
            ValidateHandle(tokenSymbol.Value, _tokenSymbols, nameof(symbol));
        }
        else if (symbol.IsNonterminal)
        {
            var nonterminal = (NonterminalHandle)symbol;
            ValidateHandle(nonterminal.Value, _nonterminals, nameof(symbol));
        }
        else
        {
            ThrowHelpers.ThrowArgumentException(nameof(symbol), "A special name must be assigned to a token symbol or nonterminal.");
        }

        if (!(_presentSpecialNames ??= new()).Add(name))
        {
            ThrowHelpers.ThrowArgumentException(nameof(name), "Cannot assign the same special name to multiple symbols.");
        }

        (_specialNames ??= new()).Add(new() { Name = name, Symbol = symbol });
    }

    private readonly TableKinds PresentTables =>
        TableKinds.Grammar
        | (_tokenSymbols is { Count: > 0 } ? TableKinds.TokenSymbol : 0)
        | (_groups is { Count: > 0 } ? TableKinds.Group : 0)
        | (_groupNestingGroups is { Count: > 0 } ? TableKinds.GroupNesting : 0)
        | (_nonterminals is { Count: > 0 } ? TableKinds.Nonterminal : 0)
        | (_productionMemberCounts is { Count: > 0 } ? TableKinds.Production : 0)
        | (_productionMemberMembers is { Count: > 0 } ? TableKinds.ProductionMember : 0)
        | (_stateMachines is { Count: > 0 } ? TableKinds.StateMachine : 0)
        | (_specialNames is { Count: > 0 } ? TableKinds.SpecialName : 0);

    public readonly void WriteTo(IBufferWriter<byte> writer, GrammarHeapSizes heapSizes)
    {
        if (!_isGrammarRowSet)
        {
            ThrowHelpers.ThrowInvalidOperationException("Grammar info have not been set.");
        }
        ValidateRequiredRowCount(_groupNestingGroups, _requiredGroupNestings, "Not enough group nestings have been added.");
        ValidateRequiredRowCount(_productionMemberCounts, _requiredProductions, "Not enough productions have been added.");
        ValidateRequiredRowCount(_productionMemberMembers, _requiredProductionMembers, "Not enough production members have been added.");

        const int grammarRows = 1;
        int tokenSymbolRows = _tokenSymbols?.Count ?? 0;
        int groupRows = _groups?.Count ?? 0;
        int groupNestingRows = _groupNestingGroups?.Count ?? 0;
        int nonterminalRows = _nonterminals?.Count ?? 0;
        int productionRows = _productionMemberCounts?.Count ?? 0;
        int productionMemberRows = _productionMemberMembers?.Count ?? 0;
        int stateMachineRows = _stateMachines?.Count ?? 0;
        int specialNameRows = _specialNames?.Count ?? 0;

        byte blobHeapIndexSize = (byte)((heapSizes & GrammarHeapSizes.BlobHeapSmall) != 0 ? 2 : 4);
        byte stringHeapIndexSize = (byte)((heapSizes & GrammarHeapSizes.StringHeapSmall) != 0 ? 2 : 4);

        byte tokenSymbolIndexSize = GrammarTables.GetIndexSize(tokenSymbolRows);
        byte groupIndexSize = GrammarTables.GetIndexSize(groupRows);
        byte groupNestingIndexSize = GrammarTables.GetIndexSize(groupNestingRows);
        byte nonterminalIndexSize = GrammarTables.GetIndexSize(nonterminalRows);
        byte productionIndexSize = GrammarTables.GetIndexSize(productionRows);
        byte productionMemberIndexSize = GrammarTables.GetIndexSize(productionMemberRows);

        byte symbolCodedIndexSize = GrammarTables.GetBinaryCodedIndexSize(tokenSymbolRows, nonterminalRows);

        TableKinds presentTables = PresentTables;
        int presentTableCount = BitOperationsCompat.PopCount((ulong)presentTables);

        writer.Write((ulong)presentTables);

        WriteRowCount(grammarRows);
        WriteRowCount(tokenSymbolRows);
        WriteRowCount(groupRows);
        WriteRowCount(groupNestingRows);
        WriteRowCount(nonterminalRows);
        WriteRowCount(productionRows);
        WriteRowCount(productionMemberRows);
        WriteRowCount(stateMachineRows);
        WriteRowCount(specialNameRows);

        WriteRowSize(grammarRows, stringHeapIndexSize + nonterminalIndexSize + sizeof(ushort));
        WriteRowSize(tokenSymbolRows, stringHeapIndexSize + sizeof(uint));
        WriteRowSize(groupRows, stringHeapIndexSize + 3 * tokenSymbolIndexSize + sizeof(ushort) + groupNestingIndexSize);
        WriteRowSize(groupNestingRows, groupIndexSize);
        WriteRowSize(nonterminalRows, stringHeapIndexSize + sizeof(ushort) + productionIndexSize);
        WriteRowSize(productionRows, productionMemberIndexSize);
        WriteRowSize(productionMemberRows, symbolCodedIndexSize);
        WriteRowSize(stateMachineRows, sizeof(ulong) + blobHeapIndexSize);
        WriteRowSize(specialNameRows, stringHeapIndexSize + symbolCodedIndexSize);

        writer.Write((byte)heapSizes);
        // Pad to 8 bytes.
        writer.Write(0, (3 * presentTableCount + 7) % 8);

        {
            WriteStringHandle(_grammarName);
            writer.WriteVariableSize(_grammarStartSymbol.Value, nonterminalIndexSize);
            writer.Write((ushort)_grammarFlags);
        }

        if (_tokenSymbols is not null)
        {
            foreach (var row in _tokenSymbols)
            {
                WriteStringHandle(row.Name);
                writer.Write((uint)row.Flags);
            }
        }

        if (_groups is not null)
        {
            uint firstNesting = 1;
            foreach (var row in _groups)
            {
                WriteStringHandle(row.Name);
                writer.WriteVariableSize(row.Container.Value, tokenSymbolIndexSize);
                writer.Write((ushort)row.Flags);
                writer.WriteVariableSize(row.Start.Value, tokenSymbolIndexSize);
                writer.WriteVariableSize(row.End.Value, tokenSymbolIndexSize);
                writer.WriteVariableSize(firstNesting, groupNestingIndexSize);
                firstNesting += (uint)row.NestingCount;
            }
        }

        if (_groupNestingGroups is not null)
        {
            foreach (uint group in _groupNestingGroups)
            {
                writer.WriteVariableSize(group, groupIndexSize);
            }
        }

        if (_nonterminals is not null)
        {
            uint firstProduction = 1;
            foreach (var row in _nonterminals)
            {
                WriteStringHandle(row.Name);
                writer.Write((ushort)row.Flags);
                writer.WriteVariableSize(firstProduction, productionIndexSize);
                firstProduction += (uint)row.ProductionCount;
            }
        }

        if (_productionMemberCounts is not null)
        {
            uint firstMember = 1;
            foreach (int memberCount in _productionMemberCounts)
            {
                writer.WriteVariableSize(firstMember, productionMemberIndexSize);
                firstMember += (uint)memberCount;
            }
        }

        if (_productionMemberMembers is not null)
        {
            foreach (EntityHandle symbol in _productionMemberMembers)
            {
                writer.WriteVariableSize(EncodeSymbolCodedIndex(symbol), symbolCodedIndexSize);
            }
        }

        if (_stateMachines is not null)
        {
            foreach (var row in _stateMachines)
            {
                writer.Write(row.Kind);
                writer.WriteVariableSize(row.Data.Value, blobHeapIndexSize);
            }
        }

        if (_specialNames is not null)
        {
            foreach (var row in _specialNames)
            {
                WriteStringHandle(row.Name);
                writer.WriteVariableSize(EncodeSymbolCodedIndex(row.Symbol), symbolCodedIndexSize);
            }
        }

        void WriteRowCount(int count)
        {
            if (count != 0)
            {
                writer.Write(count);
            }
        }

        void WriteRowSize(int rowCount, int rowSize)
        {
            if (rowCount != 0)
            {
                Debug.Assert((uint)rowSize <= byte.MaxValue);
                writer.Write(rowSize);
            }
        }

        void WriteStringHandle(StringHandle handle)
        {
            writer.WriteVariableSize(handle.Value, stringHeapIndexSize);
        }
    }

    private readonly struct TokenSymbolRow
    {
        public required StringHandle Name { get; init; }
        public required TokenSymbolAttributes Flags { get; init; }
    }

    private readonly struct GroupRow
    {
        public required StringHandle Name { get; init; }
        public required TokenSymbolHandle Container { get; init; }
        public required GroupAttributes Flags { get; init; }
        public required TokenSymbolHandle Start { get; init; }
        public required TokenSymbolHandle End { get; init; }
        public required int NestingCount { get; init; }
    }

    private readonly struct NonterminalRow
    {
        public required StringHandle Name { get; init; }
        public required NonterminalAttributes Flags { get; init; }
        public required int ProductionCount { get; init; }
    }

    private readonly struct StateMachineRow
    {
        public required ulong Kind { get; init; }
        public required BlobHandle Data { get; init; }
    }

    private readonly struct SpecialNameRow
    {
        public required StringHandle Name { get; init; }
        public required EntityHandle Symbol { get; init; }
    }
}
