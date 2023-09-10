// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars;

/// <summary>
/// Provides optimized access to data in the tables of a <see cref="Grammar"/>.
/// </summary>
internal readonly ref struct GrammarTablesHotData
{
#if NET7_0_OR_GREATER
    public readonly ref readonly GrammarTables GrammarTables;
#else
    private readonly Grammar _grammar;
    public ref readonly GrammarTables GrammarTables => ref _grammar.GrammarTables;
#endif
    public readonly ReadOnlySpan<byte> GrammarFile;

    public GrammarTablesHotData(Grammar grammar)
    {
        GrammarFile = grammar.GrammarFile;
#if NET7_0_OR_GREATER
        GrammarTables = ref grammar.GrammarTables;
#else
        _grammar = grammar;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolAttributes GetTokenSymbolFlags(TokenSymbolHandle symbol)
    {
        Debug.Assert(symbol.TableIndex != 0 && symbol.TableIndex <= (uint)GrammarTables.TokenSymbolRowCount);

        return GrammarTables.GetTokenSymbolFlags(GrammarFile, symbol.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetTokenSymbolStartedGroup(TokenSymbolHandle symbol)
    {
        Debug.Assert((GetTokenSymbolFlags(symbol) & TokenSymbolAttributes.GroupStart) != 0);
        return GrammarTables.GetTokenSymbolStartedGroup(GrammarFile, symbol.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTerminal(TokenSymbolHandle symbol) => GrammarTables.IsTerminal(symbol);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GroupAttributes GetGroupFlags(uint group)
    {
        Debug.Assert(group != 0 && group <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupFlags(GrammarFile, group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StringHandle GetGroupName(uint group)
    {
        Debug.Assert(group != 0 && group <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupName(GrammarFile, group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupContainer(uint group)
    {
        Debug.Assert(group != 0 && group <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupContainer(GrammarFile, group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupStart(uint group)
    {
        Debug.Assert(group != 0 && group <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupStart(GrammarFile, group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupEnd(uint group)
    {
        Debug.Assert(group != 0 && group <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupEnd(GrammarFile, group);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanGroupNest(uint outerGroup, uint innerGroup)
    {
        Debug.Assert(outerGroup != 0 && outerGroup <= (uint)GrammarTables.GroupRowCount);
        Debug.Assert(innerGroup != 0 && innerGroup <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.CanGroupNest(GrammarFile, outerGroup, innerGroup);
    }

    public NonterminalHandle GetProductionHead(ProductionHandle production)
    {
        Debug.Assert(production.TableIndex != 0 && production.TableIndex <= (uint)GrammarTables.ProductionRowCount);

        return GrammarTables.GetProductionHead(GrammarFile, production.TableIndex);
    }

    public int GetProductionMemberCount(ProductionHandle production)
    {
        Debug.Assert(production.TableIndex != 0 && production.TableIndex <= (uint)GrammarTables.ProductionRowCount);

        return GrammarTables.GetProductionMemberBounds(GrammarFile, production.TableIndex).Count;
    }
}
