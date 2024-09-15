// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Builder.OperatorPrecedence;

/// <summary>
/// Represents a collection of symbols in a grammar to be built
/// that have the same precedence and a specific type of associativity.
/// </summary>
/// <seealso cref="OperatorScope"/>
public class AssociativityGroup
{
    /// <summary>
    /// The <see cref="OperatorPrecedence.AssociativityType"/> of this group.
    /// </summary>
    public AssociativityType AssociativityType { get; }

    /// <summary>
    /// The symbols that belong to this group.
    /// </summary>
    /// <remarks>
    /// The items of this array can correspond to the following kinds of symbols:
    /// <list type="bullet">
    /// <item>Terminals, nonterminals and their equivalents, by passing their
    /// <see cref="IGrammarSymbol"/> instance.</item>
    /// <item>Literals, by passing the <see cref="string"/> of their value.</item>
    /// <item>Productions, by passing the object that was passed to
    /// <see cref="IProductionBuilder{TSelf}.WithPrecedence"/> or returned from
    /// <see cref="ProductionBuilderExtensions.WithPrecedence"/>, when building
    /// the production.</item>
    /// </list>
    /// Any other object will be ignored.
    /// </remarks>
    public ImmutableArray<object> Symbols { get; }

    private static void ValidateAssociativityType(AssociativityType associativityType)
    {
        if (associativityType is < AssociativityType.NonAssociative or > AssociativityType.PrecedenceOnly)
        {
            ThrowHelpers.ThrowArgumentException(nameof(associativityType));
        }
    }

    /// <summary>
    /// Creates an <see cref="AssociativityGroup"/>.
    /// </summary>
    /// <param name="associativityType">The <see cref="OperatorPrecedence.AssociativityType"/> of the group.</param>
    /// <param name="symbols">The symbols that belong to the group.</param>
    public AssociativityGroup(AssociativityType associativityType, params ImmutableArray<object> symbols)
    {
        ValidateAssociativityType(associativityType);
        if (symbols.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(symbols));
        }
        AssociativityType = associativityType;
        Symbols = symbols;
    }

    /// <inheritdoc cref="AssociativityGroup(OperatorPrecedence.AssociativityType, System.Collections.Immutable.ImmutableArray{object})"/>
    public AssociativityGroup(AssociativityType associativityType, params object[] symbols)
    {
        ValidateAssociativityType(associativityType);
        ArgumentNullExceptionCompat.ThrowIfNull(symbols);
        AssociativityType = associativityType;
        Symbols = [.. symbols];
    }
}

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.NonAssociative"/> associativity.
/// </summary>
public sealed class NonAssociative : AssociativityGroup
{
    /// <summary>
    /// Creates a <see cref="NonAssociative"/>.
    /// </summary>
    /// <param name="symbols">The symbols of the group.</param>
    public NonAssociative(params ImmutableArray<object> symbols) : base(AssociativityType.NonAssociative, symbols) { }

    /// <inheritdoc cref="NonAssociative(System.Collections.Immutable.ImmutableArray{object})"/>
    public NonAssociative(params object[] symbols) : base(AssociativityType.NonAssociative, symbols) { }
}

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.LeftAssociative"/> associativity.
/// </summary>
public sealed class LeftAssociative : AssociativityGroup
{
    /// <summary>
    /// Creates a <see cref="LeftAssociative"/>.
    /// </summary>
    /// <param name="symbols">The symbols of the group.</param>
    public LeftAssociative(params ImmutableArray<object> symbols) : base(AssociativityType.LeftAssociative, symbols) { }

    /// <inheritdoc cref="LeftAssociative(System.Collections.Immutable.ImmutableArray{object})"/>
    public LeftAssociative(params object[] symbols) : base(AssociativityType.LeftAssociative, symbols) { }
}

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.RightAssociative"/> associativity.
/// </summary>
public sealed class RightAssociative : AssociativityGroup
{
    /// <summary>
    /// Creates a <see cref="RightAssociative"/>.
    /// </summary>
    /// <param name="symbols">The symbols of the group.</param>
    public RightAssociative(params ImmutableArray<object> symbols) : base(AssociativityType.RightAssociative, symbols) { }

    /// <inheritdoc cref="RightAssociative(System.Collections.Immutable.ImmutableArray{object})"/>
    public RightAssociative(params object[] symbols) : base(AssociativityType.RightAssociative, symbols) { }
}

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.PrecedenceOnly"/> associativity.
/// </summary>
public sealed class PrecedenceOnly : AssociativityGroup
{
    /// <summary>
    /// Creates a <see cref="PrecedenceOnly"/>.
    /// </summary>
    /// <param name="symbols">The symbols of the group.</param>
    public PrecedenceOnly(params ImmutableArray<object> symbols) : base(AssociativityType.PrecedenceOnly, symbols) { }

    /// <inheritdoc cref="NonAssociative(System.Collections.Immutable.ImmutableArray{object})"/>
    public PrecedenceOnly(params object[] symbols) : base(AssociativityType.PrecedenceOnly, symbols) { }
}
