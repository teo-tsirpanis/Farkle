// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Represents a nonterminal of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// A nonterminal is a composite symbol that can be derived
/// from a sequence of terminals and other nonterminals, as
/// specified by its <see cref="Productions"/>.
/// </remarks>
public readonly struct Nonterminal
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="Nonterminal"/>'s <see cref="NonterminalHandle"/>.
    /// </summary>
    public NonterminalHandle Handle { get; }

    internal Nonterminal(Grammar grammar, NonterminalHandle handle)
    {
        _grammar = grammar;
        Handle = handle;
    }

    [StackTraceHidden]
    private void AssertHasValue()
    {
        Debug.Assert(_grammar is not null);
        if (!Handle.HasValue)
        {
            ThrowHelpers.ThrowHandleHasNoValue();
        }
    }

    internal (uint Offset, uint NextOffset) GetProductionsBounds(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        uint tableIndex = Handle.TableIndex;
        uint firstProduction = grammarTables.GetNonterminalFirstProduction(grammarFile, tableIndex).TableIndex;
        uint firstProductionOfNext = tableIndex < (uint)grammarTables.NonterminalRowCount ? grammarTables.GetNonterminalFirstProduction(grammarFile, tableIndex + 1).TableIndex : (uint)grammarTables.ProductionRowCount + 1;
        Debug.Assert(firstProduction <= firstProductionOfNext);
        return (firstProduction, firstProductionOfNext);
    }

    /// <summary>
    /// A <see cref="StringHandle"/> pointing to the <see cref="Nonterminal"/>'s name.
    /// </summary>
    public StringHandle Name
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetNonterminalName(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    /// <summary>
    /// The <see cref="Nonterminal"/>'s <see cref="NonterminalAttributes"/>.
    /// </summary>
    public NonterminalAttributes Attributes
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetNonterminalFlags(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    /// <summary>
    /// A sequence of the <see cref="Nonterminal"/>'s <see cref="Production"/>s.
    /// </summary>
    public ProductionCollection Productions
    {
        get
        {
            AssertHasValue();
            (uint offset, uint nextOffset) = GetProductionsBounds(_grammar.GrammarFile, in _grammar.GrammarTables);
            return new(_grammar, offset, (int)(nextOffset - offset));
        }
    }

    /// <summary>
    /// Returns a string describing the the <see cref="Nonterminal"/>.
    /// </summary>
    public override string ToString() => $"<{_grammar.GetString(Name)}>";
}
