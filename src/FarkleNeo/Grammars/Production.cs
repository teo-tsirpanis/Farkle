// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Represents a production of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// A production consists of a "head" <see cref="Nonterminal"/>
/// and a (possibly empty) sequence of terminals or nonterminals
/// such that when these symbols are encountered, the head
/// nonterminal can be derived.
/// </remarks>
/// <seealso cref="Grammar.Productions"/>
/// <seealso cref="Nonterminal.Productions"/>
public readonly struct Production
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="Production"/>'s <see cref="ProductionHandle"/>.
    /// </summary>
    public ProductionHandle Handle { get; }

    internal Production(Grammar grammar, ProductionHandle handle)
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

    internal (uint Offset, uint NextOffset) GetMemberBounds(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        uint tableIndex = Handle.TableIndex;
        uint firstNesting = grammarTables.GetProductionFirstMember(grammarFile, tableIndex);
        uint firstNestingOfNext = tableIndex < (uint)grammarTables.ProductionRowCount - 1 ? grammarTables.GetProductionFirstMember(grammarFile, tableIndex + 1) : (uint)grammarTables.ProductionRowCount;
        Debug.Assert(firstNesting <= firstNestingOfNext);
        return (firstNesting, firstNestingOfNext);
    }

    /// <summary>
    /// Gets a <see cref="NonterminalHandle"/> pointing to the <see cref="Production"/>'s head.
    /// </summary>
    /// <remarks>
    /// This method's implementation has a non-constant time complexity, relative to the
    /// number of nonterminals in the grammar. If you are calling this method in a loop,
    /// you should instead start from a <see cref="Nonterminal"/> and enumerate its
    /// <see cref="Nonterminal.Productions"/>. Farkle's grammar format is not optimized
    /// for quick lookups of a production's head.
    /// </remarks>
    public NonterminalHandle GetHead()
    {
        AssertHasValue();
        Debug.Assert(Handle.Value < _grammar.GrammarTables.ProductionRowCount);

        ref readonly GrammarTables grammarTables = ref _grammar.GrammarTables;
        ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;
        for (uint i = 2; i <= grammarTables.NonterminalRowCount; i++)
        {
            if (Handle.TableIndex < grammarTables.GetNonterminalFirstProduction(grammarFile, i).TableIndex)
            {
                return new(i - 1);
            }
        }

        return new((uint)grammarTables.NonterminalRowCount);
    }

    /// <summary>
    /// A sequence of the <see cref="Production"/>'s members.
    /// </summary>
    public ProductionMemberCollection Members
    {
        get
        {
            AssertHasValue();
            (uint offset, uint nextOffset) = GetMemberBounds(_grammar.GrammarFile, in _grammar.GrammarTables);
            return new(_grammar, offset, (int)(nextOffset - offset));
        }
    }
}
