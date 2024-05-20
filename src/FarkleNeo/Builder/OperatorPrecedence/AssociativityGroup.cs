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
    /// The <see cref="OperatorPrecedence.AssociativityType"/> of the group.
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
    public AssociativityGroup(AssociativityType associativityType, ImmutableArray<object> symbols)
    {
        ValidateAssociativityType(associativityType);
        if (symbols.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(symbols));
        }
        AssociativityType = associativityType;
        Symbols = symbols;
    }

    /// <summary>
    /// Creates an <see cref="AssociativityGroup"/>.
    /// </summary>
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
/// <param name="symbols">The symbols of the group.</param>
public class NonAssociative(params object[] symbols) : AssociativityGroup(AssociativityType.NonAssociative, symbols);

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.LeftAssociative"/> associativity.
/// </summary>
/// <param name="symbols">The symbols of the group.</param>
public class LeftAssociative(params object[] symbols) : AssociativityGroup(AssociativityType.LeftAssociative, symbols);

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.RightAssociative"/> associativity.
/// </summary>
/// <param name="symbols">The symbols of the group.</param>
public class RightAssociative(params object[] symbols) : AssociativityGroup(AssociativityType.RightAssociative, symbols);

/// <summary>
/// Provides a shortcut to create <see cref="AssociativityGroup"/>s with
/// <see cref="AssociativityType.PrecedenceOnly"/> associativity.
/// </summary>
/// <param name="symbols">The symbols of the group.</param>
public class PrecedenceOnly(params object[] symbols) : AssociativityGroup(AssociativityType.PrecedenceOnly, symbols);
