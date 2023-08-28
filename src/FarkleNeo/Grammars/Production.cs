// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text;

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
        uint firstMember = grammarTables.GetProductionFirstMember(grammarFile, tableIndex);
        uint firstMemberOfNext = tableIndex < (uint)grammarTables.ProductionRowCount ? grammarTables.GetProductionFirstMember(grammarFile, tableIndex + 1) : (uint)grammarTables.ProductionMemberRowCount + 1;
        Debug.Assert(firstMember <= firstMemberOfNext);
        return (firstMember, firstMemberOfNext);
    }

    /// <summary>
    /// A <see cref="NonterminalHandle"/> pointing to the <see cref="Production"/>'s head.
    /// </summary>
    public NonterminalHandle Head
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetProductionHead(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    /// <summary>
    /// A list of the <see cref="Production"/>'s members.
    /// </summary>
    public ProductionMemberList Members
    {
        get
        {
            AssertHasValue();
            (uint offset, uint nextOffset) = GetMemberBounds(_grammar.GrammarFile, in _grammar.GrammarTables);
            return new(_grammar, offset, (int)(nextOffset - offset));
        }
    }

    /// <summary>
    /// Returns a string describing the the <see cref="Production"/>.
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append(_grammar.GetNonterminal(Head));
        sb.Append(" ::=");
        foreach (EntityHandle member in Members)
        {
            sb.Append(' ');
            if (member.IsTokenSymbol)
            {
                sb.Append(_grammar.GetTokenSymbol((TokenSymbolHandle)member));
            }
            else
            {
                sb.Append(_grammar.GetNonterminal((NonterminalHandle)member));
            }
        }

        return sb.ToString();
    }
}
