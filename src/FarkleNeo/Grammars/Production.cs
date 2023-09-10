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
            (uint offset, int count) = _grammar.GrammarTables.GetProductionMemberBounds(_grammar.GrammarFile, Handle.TableIndex);
            return new(_grammar, offset, count);
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
