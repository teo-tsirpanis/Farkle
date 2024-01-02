// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Collections.Immutable;

namespace Farkle.Grammars.Writers;

internal sealed class GrammarWriter
{
    private StringHeapWriter _stringHeapWriter;

    private BlobHeapWriter _blobHeapWriter;

    private GrammarTablesWriter _tablesWriter;

    private static void ValidateTableIndex(uint tableIndex, string paramName)
    {
        if (tableIndex == 0)
            ThrowHelpers.ThrowArgumentNullException(paramName);
    }

    private GrammarHeapSizes HeapSizes =>
        (_stringHeapWriter.LengthSoFar <= ushort.MaxValue ? GrammarHeapSizes.StringHeapSmall : 0)
        | (_blobHeapWriter.LengthSoFar <= ushort.MaxValue ? GrammarHeapSizes.BlobHeapSmall : 0);

    private int StreamCount =>
        1 // We always write a table stream.
        + (_stringHeapWriter.LengthSoFar > 0 ? 1 : 0)
        + (_blobHeapWriter.LengthSoFar > 0 ? 1 : 0);

    public StringHandle GetOrAddString(string str)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(str);

        return _stringHeapWriter.Add(str);
    }

    public BlobHandle GetOrAddBlob(PooledSegmentBufferWriter<byte> blob) =>
        _blobHeapWriter.Add(blob);

    public BlobHandle GetOrAddBlob(ReadOnlySpan<byte> blob) =>
        _blobHeapWriter.Add(blob);

    public void SetGrammarInfo(StringHandle name, NonterminalHandle startSymbol, GrammarAttributes flags)
    {
        _stringHeapWriter.ValidateHandle(name);
        ValidateTableIndex(startSymbol.TableIndex, nameof(startSymbol));
        _tablesWriter.SetGrammarInfo(name, startSymbol, flags);
    }

    public TokenSymbolHandle AddTokenSymbol(StringHandle name, TokenSymbolAttributes flags)
    {
        _stringHeapWriter.ValidateHandle(name);
        return _tablesWriter.AddTokenSymbol(name, flags);
    }

    public uint AddGroup(StringHandle name, TokenSymbolHandle container, GroupAttributes flags, TokenSymbolHandle start, TokenSymbolHandle end, int nestingCount)
    {
        _stringHeapWriter.ValidateHandle(name);
        ValidateTableIndex(container.TableIndex, nameof(container));
        ValidateTableIndex(start.TableIndex, nameof(start));
        ValidateTableIndex(end.TableIndex, nameof(end));
        return _tablesWriter.AddGroup(name, container, flags, start, end, nestingCount);
    }

    public void AddGroupNesting(uint groupIndex)
    {
        ValidateTableIndex(groupIndex, nameof(groupIndex));
        _tablesWriter.AddGroupNesting(groupIndex);
    }

    public NonterminalHandle AddNonterminal(StringHandle name, NonterminalAttributes flags, int productionCount)
    {
        _stringHeapWriter.ValidateHandle(name);
        return _tablesWriter.AddNonterminal(name, flags, productionCount);
    }

    public ProductionHandle AddProduction(int memberCount)
    {
        return _tablesWriter.AddProduction(memberCount);
    }

    public void AddProductionMember(EntityHandle member)
    {
        ValidateTableIndex(member.TableIndex, nameof(member));
        _tablesWriter.AddProductionMember(member);
    }

    public void AddStateMachine(DfaWriter<char> dfa)
    {
        using var buffer = new PooledSegmentBufferWriter<byte>();

        dfa.WriteDfaData(buffer, _tablesWriter.TokenSymbolRowCount);
        ulong dfaDataKind = dfa.HasConflicts ? GrammarConstants.DfaOnCharWithConflictsKind : GrammarConstants.DfaOnCharKind;
        AddStateMachine(dfaDataKind, GetOrAddBlob(buffer));

        if (dfa.HasDefaultTransitions)
        {
            buffer.Clear();
            dfa.WriteDefaultTransitions(buffer);
            AddStateMachine(GrammarConstants.DfaOnCharDefaultTransitionsKind, GetOrAddBlob(buffer));
        }
    }

    public void AddStateMachine(LrWriter lr)
    {
        using var buffer = new PooledSegmentBufferWriter<byte>();

        lr.WriteData(buffer, _tablesWriter.TokenSymbolRowCount, _tablesWriter.TerminalCount, _tablesWriter.ProductionCount, _tablesWriter.NonterminalCount);
        ulong kind = lr.HasConflicts ? GrammarConstants.Glr1Kind : GrammarConstants.Lr1Kind;
        AddStateMachine(kind, GetOrAddBlob(buffer));
    }

    public void AddStateMachine(ulong kind, BlobHandle data)
    {
        _blobHeapWriter.ValidateHandle(data);
        _tablesWriter.AddStateMachine(kind, data);
    }

    public void AddSpecialName(StringHandle name, EntityHandle symbol)
    {
        _stringHeapWriter.ValidateHandle(name);
        ValidateTableIndex(symbol.TableIndex, nameof(symbol));
        _tablesWriter.AddSpecialName(name, symbol);
    }

    public void WriteTo(IBufferWriter<byte> writer)
    {
        using PooledSegmentBufferWriter<byte> tablesBuffer = new();
        _tablesWriter.WriteTo(tablesBuffer, HeapSizes);

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

        bool writeStringHeap = _stringHeapWriter.LengthSoFar > 0;
        bool writeBlobHeap = _blobHeapWriter.LengthSoFar > 0;

        if (writeStringHeap)
        {
            int length = _stringHeapWriter.LengthSoFar;
            writer.Write(GrammarConstants.StringHeapIdentifier);
            writer.Write(dataOffset);
            writer.Write(length);
            dataOffset += length;
        }

        if (writeBlobHeap)
        {
            int length = _blobHeapWriter.LengthSoFar;
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
            _stringHeapWriter.WriteTo(writer);
        }

        if (writeBlobHeap)
        {
            _blobHeapWriter.WriteTo(writer);
        }

        tablesBuffer.WriteTo(ref writer);
    }

    public ImmutableArray<byte> ToImmutableArray()
    {
        using var buffer = new PooledSegmentBufferWriter<byte>();
        WriteTo(buffer);
        return buffer.ToImmutableArray();
    }
}
