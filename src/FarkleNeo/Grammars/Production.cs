// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text;

namespace Farkle.Grammars;

/// <summary>
/// Represents a production of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// A production is a rule of the form <c>A ::= b</c>, where <c>A</c> (the <see cref="Head"/>) is a <see cref="Nonterminal"/>
/// and b (the <see cref="Members"/>) is a possibly empty sequence of terminals or nonterminals such that when the right-hand
/// side symbols are encountered, they can derive and be substituted by the left-hand side symbol.
/// </remarks>
/// <seealso cref="Grammar.Productions"/>
/// <seealso cref="Nonterminal.Productions"/>
public readonly struct Production : IEquatable<Production>
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="Production"/>'s <see cref="ProductionHandle"/>.
    /// </summary>
    /// <remarks>
    /// In earlier versions of Farkle the <c>Handle</c> property referred
    /// to the property that is now called <see cref="Members"/>.
    /// </remarks>
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
    /// The nonterminal on the <see cref="Production"/>'s left-hand side.
    /// </summary>
    public Nonterminal Head
    {
        get
        {
            AssertHasValue();
            return new(_grammar, _grammar.GrammarTables.GetProductionHead(_grammar.GrammarFile, Handle.TableIndex));
        }
    }

    /// <summary>
    /// The terminals or nonterminals on the <see cref="Production"/>'s right-hand side.
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

    /// <inheritdoc/>
    public bool Equals(Production other) => _grammar == other._grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Production other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="Production"/>.
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append(Head);
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

    /// <summary>
    /// Compares two <see cref="Production"/>s for equality.
    /// </summary>
    /// <param name="left">The first production.</param>
    /// <param name="right">The second production.</param>
    public static bool operator ==(Production left, Production right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Production"/>s for inequality.
    /// </summary>
    /// <param name="left">The first production.</param>
    /// <param name="right">The second production.</param>
    public static bool operator !=(Production left, Production right) => !left.Equals(right);
}
