// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;

namespace Farkle.Grammars;

internal sealed class GrammarBuilder
{
    private StringHeapBuilder _stringHeapBuilder;

    private BlobHeapBuilder _blobHeapBuilder;

    private GrammarTablesBuilder _tablesBuilder;

    private static void ValidateTableIndex(uint tableIndex, string paramName)
    {
        if (tableIndex == 0)
            ThrowHelpers.ThrowArgumentNullException(paramName);
    }

    private GrammarHeapSizes HeapSizes =>
        (_stringHeapBuilder.LengthSoFar <= ushort.MaxValue ? GrammarHeapSizes.StringHeapSmall : 0)
        | (_blobHeapBuilder.LengthSoFar <= ushort.MaxValue ? GrammarHeapSizes.BlobHeapSmall : 0);

    private int StreamCount =>
        1 // We always write a table stream.
        + (_stringHeapBuilder.LengthSoFar > 0 ? 1 : 0)
        + (_blobHeapBuilder.LengthSoFar > 0 ? 1 : 0);

    public StringHandle GetOrAddString(string str)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(str);

        return _stringHeapBuilder.Add(str);
    }

    public BlobHandle GetOrAddBlob(PooledSegmentBufferWriter<byte> blob) =>
        _blobHeapBuilder.Add(blob);

    public BlobHandle GetOrAddBlob(ReadOnlySpan<byte> blob) =>
        _blobHeapBuilder.Add(blob);

    public void SetGrammarInfo(StringHandle name, NonterminalHandle startSymbol, GrammarAttributes flags)
    {
        _stringHeapBuilder.ValidateHandle(name);
        ValidateTableIndex(startSymbol.TableIndex, nameof(startSymbol));
        _tablesBuilder.SetGrammarInfo(name, startSymbol, flags);
    }

    public TokenSymbolHandle AddTokenSymbol(StringHandle name, TokenSymbolAttributes flags)
    {
        _stringHeapBuilder.ValidateHandle(name);
        return _tablesBuilder.AddTokenSymbol(name, flags);
    }

    public uint AddGroup(StringHandle name, TokenSymbolHandle container, GroupAttributes flags, TokenSymbolHandle start, TokenSymbolHandle end, int nestingCount)
    {
        _stringHeapBuilder.ValidateHandle(name);
        ValidateTableIndex(container.TableIndex, nameof(container));
        ValidateTableIndex(start.TableIndex, nameof(start));
        ValidateTableIndex(end.TableIndex, nameof(end));
        return _tablesBuilder.AddGroup(name, container, flags, start, end, nestingCount);
    }

    public void AddGroupNesting(uint groupIndex)
    {
        ValidateTableIndex(groupIndex, nameof(groupIndex));
        _tablesBuilder.AddGroupNesting(groupIndex);
    }

    public NonterminalHandle AddNonterminal(StringHandle name, NonterminalAttributes flags, int productionCount)
    {
        _stringHeapBuilder.ValidateHandle(name);
        return _tablesBuilder.AddNonterminal(name, flags, productionCount);
    }

    public ProductionHandle AddProduction(int memberCount)
    {
        return _tablesBuilder.AddProduction(memberCount);
    }

    public void AddProductionMember(EntityHandle member)
    {
        ValidateTableIndex(member.TableIndex, nameof(member));
        _tablesBuilder.AddProductionMember(member);
    }

    public void AddStateMachine(DfaBuilder<char> dfa)
    {
        using var buffer = new PooledSegmentBufferWriter<byte>();

        dfa.WriteDfaData(buffer, _tablesBuilder.TokenSymbolRowCount);
        ulong dfaDataKind = dfa.HasConflicts ? GrammarConstants.DfaOnCharWithConflictsKind : GrammarConstants.DfaOnCharKind;
        AddStateMachine(dfaDataKind, GetOrAddBlob(buffer));

        if (dfa.HasDefaultTransitions)
        {
            buffer.Clear();
            dfa.WriteDefaultTransitions(buffer);
            AddStateMachine(GrammarConstants.DfaOnCharDefaultTransitionsKind, GetOrAddBlob(buffer));
        }
    }

    public void AddStateMachine(LrBuilder lr)
    {
        using var buffer = new PooledSegmentBufferWriter<byte>();

        lr.WriteData(buffer, _tablesBuilder.TokenSymbolRowCount, _tablesBuilder.TerminalCount, _tablesBuilder.ProductionCount, _tablesBuilder.NonterminalCount);
        ulong kind = lr.HasConflicts ? GrammarConstants.Lr1Kind : GrammarConstants.Glr1Kind;
        AddStateMachine(kind, GetOrAddBlob(buffer));
    }

    public void AddStateMachine(ulong kind, BlobHandle data)
    {
        _blobHeapBuilder.ValidateHandle(data);
        _tablesBuilder.AddStateMachine(kind, data);
    }

    public void AddSpecialName(StringHandle name, EntityHandle symbol)
    {
        _stringHeapBuilder.ValidateHandle(name);
        ValidateTableIndex(symbol.TableIndex, nameof(symbol));
        _tablesBuilder.AddSpecialName(name, symbol);
    }

    public void WriteTo(IBufferWriter<byte> writer)
    {
        using PooledSegmentBufferWriter<byte> tablesBuffer = new();
        _tablesBuilder.WriteTo(tablesBuffer, HeapSizes);

        if (tablesBuffer.WrittenCount > int.MaxValue)
        {
            ThrowHelpers.ThrowOutOfMemoryException();
        }

        writer.Write(GrammarConstants.HeaderMagic);
        writer.Write(GrammarConstants.VersionMajor);
        writer.Write(GrammarConstants.VersionMinor);
        writer.Write((uint)StreamCount);

        int dataOffset =
            sizeof(ulong) + 2 * sizeof(ushort) + sizeof(uint)
            + StreamCount * (sizeof(ulong) + 2 * sizeof(int));

        bool writeStringHeap = _stringHeapBuilder.LengthSoFar > 0;
        bool writeBlobHeap = _blobHeapBuilder.LengthSoFar > 0;

        if (writeStringHeap)
        {
            int length = _stringHeapBuilder.LengthSoFar;
            writer.Write(GrammarConstants.StringHeapIdentifier);
            writer.Write(dataOffset);
            writer.Write(length);
            dataOffset += length;
        }

        if (writeBlobHeap)
        {
            int length = _blobHeapBuilder.LengthSoFar;
            writer.Write(GrammarConstants.BlobHeapIdentifier);
            writer.Write(dataOffset);
            writer.Write(length);
            dataOffset += length;
        }

        writer.Write(GrammarConstants.TableStreamIdentifier);
        writer.Write(dataOffset);
        writer.Write((int)tablesBuffer.WrittenCount);

        if (writeStringHeap)
        {
            _stringHeapBuilder.WriteTo(writer);
        }

        if (writeBlobHeap)
        {
            _blobHeapBuilder.WriteTo(writer);
        }

        tablesBuffer.WriteTo(ref writer);
    }
}
