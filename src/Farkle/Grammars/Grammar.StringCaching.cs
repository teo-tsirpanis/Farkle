// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

public partial class Grammar
{
    private string?[]? _tokenSymbolNames, _nonterminalNames, _groupNames, _specialNameNames;

    private string? _grammarName;

    private static ref string? GetTableNameReference(ref string?[]? array, int rowCount, uint tableIndex)
    {
        if (Volatile.Read(ref array) is not { } arr)
        {
            Interlocked.CompareExchange(ref array, new string[rowCount], null);
            arr = array;
        }

        return ref arr[(int)(tableIndex - 1)];
    }

    internal string GetTokenSymbolName(TokenSymbolHandle handle)
    {
        ref string? name = ref GetTableNameReference(ref _tokenSymbolNames, GrammarTables.TokenSymbolRowCount, handle.TableIndex);
        if (Volatile.Read(ref name) is not { } n)
        {
            ReadOnlySpan<byte> grammarFile = GrammarFile;
            StringHandle nameHandle = GrammarTables.GetTokenSymbolName(grammarFile, handle.TableIndex);
            string newName = StringHeap.GetString(grammarFile, nameHandle);
            Interlocked.CompareExchange(ref name, newName, null);
            n = name;
        }

        return n;
    }

    internal string GetNonterminalName(NonterminalHandle handle)
    {
        ref string? name = ref GetTableNameReference(ref _nonterminalNames, GrammarTables.NonterminalRowCount, handle.TableIndex);
        if (Volatile.Read(ref name) is not { } n)
        {
            ReadOnlySpan<byte> grammarFile = GrammarFile;
            StringHandle nameHandle = GrammarTables.GetNonterminalName(grammarFile, handle.TableIndex);
            string newName = StringHeap.GetString(grammarFile, nameHandle);
            Interlocked.CompareExchange(ref name, newName, null);
            n = name;
        }

        return n;
    }

    internal string GetGroupName(GroupHandle handle)
    {
        ref string? name = ref GetTableNameReference(ref _groupNames, GrammarTables.GroupRowCount, handle.TableIndex);
        if (Volatile.Read(ref name) is not { } n)
        {
            ReadOnlySpan<byte> grammarFile = GrammarFile;
            StringHandle nameHandle = GrammarTables.GetGroupName(grammarFile, handle.TableIndex);
            string newName = StringHeap.GetString(grammarFile, nameHandle);
            Interlocked.CompareExchange(ref name, newName, null);
            n = name;
        }

        return n;
    }

    internal string GetSpecialNameName(uint index)
    {
        ref string? name = ref GetTableNameReference(ref _specialNameNames, GrammarTables.SpecialNameRowCount, index);
        if (Volatile.Read(ref name) is not { } n)
        {
            ReadOnlySpan<byte> grammarFile = GrammarFile;
            StringHandle nameHandle = GrammarTables.GetSpecialNameName(grammarFile, index);
            string newName = StringHeap.GetString(grammarFile, nameHandle);
            Interlocked.CompareExchange(ref name, newName, null);
            n = name;
        }

        return n;
    }

    internal string GetGrammarName()
    {
        if (Volatile.Read(ref _grammarName) is not { } n)
        {
            ReadOnlySpan<byte> grammarFile = GrammarFile;
            StringHandle nameHandle = GrammarTables.GetGrammarName(grammarFile);
            string newName = StringHeap.GetString(grammarFile, nameHandle);
            Interlocked.CompareExchange(ref _grammarName, newName, null);
            n = _grammarName;
        }

        return n;
    }
}
