// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars;

/// <summary>
/// Represents an entry in the <c>SpecialName</c> table of a <see cref="Grammar"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
public readonly struct SpecialNameDefinition : IEquatable<SpecialNameDefinition>
{
    private readonly Grammar _grammar;

    internal uint Index { get; }

    internal SpecialNameDefinition(Grammar grammar, uint tableIndex)
    {
        _grammar = grammar;
        Index = tableIndex;
    }

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay() => _grammar is null ? "<null>" : $"Name = {Name}; Symbol = {_grammar.GetEntity(Symbol)}";

    [StackTraceHidden]
    private void AssertHasValue()
    {
        Debug.Assert(_grammar is not null);
        if (Index == 0)
        {
            ThrowHelpers.ThrowHandleHasNoValue();
        }
    }

    /// <summary>
    /// The <see cref="SpecialNameDefinition"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            AssertHasValue();
            return _grammar.GetSpecialNameName(Index);
        }
    }

    /// <summary>
    /// The
    /// </summary>
    public EntityHandle Symbol
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetSpecialNameSymbol(_grammar.GrammarFile, Index);
        }
    }

    /// <inheritdoc/>
    public bool Equals(SpecialNameDefinition other) => _grammar == other._grammar && Index == other.Index;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SpecialNameDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Index);

    /// <summary>
    /// Compares two <see cref="SpecialNameDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first special name definition.</param>
    /// <param name="right">The second special name definition.</param>
    public static bool operator ==(SpecialNameDefinition left, SpecialNameDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="SpecialNameDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first special name definition.</param>
    /// <param name="right">The second special name definition.</param>
    public static bool operator !=(SpecialNameDefinition left, SpecialNameDefinition right) => !left.Equals(right);
}
