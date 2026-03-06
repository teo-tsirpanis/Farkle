// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a nonterminal of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// A nonterminal is a composite symbol that can be derived
/// from a sequence of terminals and other nonterminals, as
/// specified by its <see cref="Productions"/>.
/// </remarks>
public readonly struct NonterminalDefinition : IEquatable<NonterminalDefinition>
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="NonterminalDefinition"/>'s <see cref="NonterminalHandle"/>.
    /// </summary>
    public NonterminalHandle Handle { get; }

    internal NonterminalDefinition(Grammar grammar, NonterminalHandle handle)
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
    /// The <see cref="NonterminalDefinition"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            AssertHasValue();
            return _grammar.GetNonterminalName(Handle);
        }
    }

    /// <summary>
    /// The <see cref="NonterminalDefinition"/>'s <see cref="NonterminalAttributes"/>.
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
    /// The <see cref="ProductionDefinition"/>s that have this <see cref="NonterminalDefinition"/> as their <see cref="ProductionDefinition.Head"/>.
    /// </summary>
    public ProductionDefinitionCollection Productions
    {
        get
        {
            AssertHasValue();
            (uint offset, int count) =_grammar.GrammarTables.GetNonterminalProductionBounds(_grammar.GrammarFile, Handle.TableIndex);
            return new(_grammar, offset, count);
        }
    }

    /// <inheritdoc/>
    public bool Equals(NonterminalDefinition other) => _grammar == other._grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NonterminalDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="NonterminalDefinition"/>.
    /// </summary>
    public override string ToString() => _grammar is null ? "" : $"<{Name}>";

    /// <summary>
    /// Compares two <see cref="NonterminalDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first nonterminal.</param>
    /// <param name="right">The second nonterminal.</param>
    public static bool operator ==(NonterminalDefinition left, NonterminalDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="NonterminalDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first nonterminal.</param>
    /// <param name="right">The second nonterminal.</param>
    public static bool operator !=(NonterminalDefinition left, NonterminalDefinition right) => !left.Equals(right);
}
