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
    public GroupHandle GetTokenSymbolStartedGroup(TokenSymbolHandle symbol)
    {
        Debug.Assert((GetTokenSymbolFlags(symbol) & TokenSymbolAttributes.GroupStart) != 0);
        return GrammarTables.GetTokenSymbolStartedGroup(GrammarFile, symbol.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTerminal(TokenSymbolHandle symbol) => GrammarTables.IsTerminal(symbol);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GroupAttributes GetGroupFlags(GroupHandle group)
    {
        Debug.Assert(group.TableIndex != 0 && group.TableIndex <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupFlags(GrammarFile, group.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupContainer(GroupHandle group)
    {
        Debug.Assert(group.TableIndex != 0 && group.TableIndex <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupContainer(GrammarFile, group.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupStart(GroupHandle group)
    {
        Debug.Assert(group.TableIndex != 0 && group.TableIndex <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupStart(GrammarFile, group.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenSymbolHandle GetGroupEnd(GroupHandle group)
    {
        Debug.Assert(group.TableIndex != 0 && group.TableIndex <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.GetGroupEnd(GrammarFile, group.TableIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanGroupNest(GroupHandle outerGroup, GroupHandle innerGroup)
    {
        Debug.Assert(outerGroup.TableIndex != 0 && outerGroup.TableIndex <= (uint)GrammarTables.GroupRowCount);
        Debug.Assert(innerGroup.TableIndex != 0 && innerGroup.TableIndex <= (uint)GrammarTables.GroupRowCount);

        return GrammarTables.CanGroupNest(GrammarFile, outerGroup.TableIndex, innerGroup.TableIndex);
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
