// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a production of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// A production is a rule of the form <c>A ::= b</c>, where <c>A</c> (the <see cref="Head"/>) is a <see cref="NonterminalDefinition"/>
/// and b (the <see cref="Members"/>) is a possibly empty sequence of terminals or nonterminals such that when the right-hand
/// side symbols are encountered, they can derive and be substituted by the left-hand side symbol.
/// </remarks>
/// <seealso cref="Grammar.Productions"/>
/// <seealso cref="NonterminalDefinition.Productions"/>
public readonly struct ProductionDefinition : IEquatable<ProductionDefinition>
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="ProductionDefinition"/>'s <see cref="ProductionHandle"/>.
    /// </summary>
    /// <remarks>
    /// In earlier versions of Farkle the <c>Handle</c> property referred
    /// to the property that is now called <see cref="Members"/>.
    /// </remarks>
    public ProductionHandle Handle { get; }

    internal ProductionDefinition(Grammar grammar, ProductionHandle handle)
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
    /// The nonterminal on the <see cref="ProductionDefinition"/>'s left-hand side.
    /// </summary>
    public NonterminalDefinition Head
    {
        get
        {
            AssertHasValue();
            return new(_grammar, _grammar.GrammarTables.GetProductionHead(_grammar.GrammarFile, Handle.TableIndex));
        }
    }

    /// <summary>
    /// The terminals or nonterminals on the <see cref="ProductionDefinition"/>'s right-hand side.
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
    public bool Equals(ProductionDefinition other) => _grammar == other._grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProductionDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="ProductionDefinition"/>.
    /// </summary>
    public override string ToString()
    {
        if (_grammar is null)
        {
            return "";
        }

        StringBuilder sb = new();

        sb.Append(Head);
        sb.Append(" ::=");
        foreach (SymbolDefinition member in Members)
        {
            sb.Append(' ');
            sb.Append(member);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compares two <see cref="ProductionDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first production.</param>
    /// <param name="right">The second production.</param>
    public static bool operator ==(ProductionDefinition left, ProductionDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="ProductionDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first production.</param>
    /// <param name="right">The second production.</param>
    public static bool operator !=(ProductionDefinition left, ProductionDefinition right) => !left.Equals(right);
}
