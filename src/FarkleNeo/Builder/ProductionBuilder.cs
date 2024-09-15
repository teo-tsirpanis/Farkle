// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Builder.ProductionBuilders;
using Farkle.Parser;

namespace Farkle.Builder;

/// <summary>
/// Provides an API for fluently building productions.
/// </summary>
/// <remarks>
/// A production builder constructs <see cref="IProduction{T}"/>s by aggregating the types
/// of its significant members. The types of the production's significant members are indicated
/// by the type parameters. For example, a <c>ProductionBuilder</c> has no significant members,
/// and a <c>ProductionBuilder&lt;int, string&gt;</c> has two significant members: an integer
/// and a string.
/// </remarks>
public sealed class ProductionBuilder : IProductionBuilder<ProductionBuilder>, IProduction
{
    private readonly ImmutableList<IGrammarSymbol> _members;

    private readonly object? _precedenceToken;

    ImmutableArray<IGrammarSymbol> IProduction.Members => _members.ToImmutableArray();

    Fuser<object?> IProduction.Fuser => (ref ParserState state, Span<object?> input) => null;

    object? IProduction.PrecedenceToken => _precedenceToken;

    void IProductionBuilder<ProductionBuilder>.MustNotImplement() { }

    /// <summary>
    /// A production builder with no members.
    /// </summary>
    public static ProductionBuilder Empty { get; } = new();

    private ProductionBuilder(ImmutableList<IGrammarSymbol> members, object? precedenceToken = null)
    {
        _members = members;
        _precedenceToken = precedenceToken;
    }

    /// <summary>
    /// Creates a production builder with the given members.
    /// </summary>
    /// <param name="members">An array of the production's members.</param>
    /// <exception cref="ArgumentException"><paramref name="members"/> has an item
    /// whose type is not <see cref="IGrammarSymbol"/>, <see cref="string"/> or
    /// <see cref="char"/>.</exception>
    public ProductionBuilder(params object[] members)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(members);

        var membersList = ImmutableList.CreateBuilder<IGrammarSymbol>();
        for (int i = 0; i < members.Length; i++)
        {
            switch (members[i])
            {
                case IGrammarSymbol symbol:
                    membersList.Add(symbol);
                    break;
                case string s:
                    membersList.Add(Terminal.Literal(s));
                    break;
                case char c:
                    membersList.Add(Terminal.Literal(c.ToString()));
                    break;
                default:
                    Fail();
                    break;
            }
            void Fail() =>
                ThrowHelpers.ThrowArgumentExceptionLocalized($"{nameof(members)}[{i}]", nameof(Resources.Builder_ProductionBuilderInvalidMemberObject));
        }

        _members = membersList.ToImmutable();
    }

    /// <inheritdoc/>
    public ProductionBuilder Append(IGrammarSymbol symbol)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return new(_members.Add(symbol), _precedenceToken);
    }

    /// <summary>
    /// Extends the production with a new significant member.
    /// </summary>
    /// <typeparam name="T1">The type of the new significant member.</typeparam>
    /// <param name="symbol">The new significant member.</param>
    /// <returns>A production builder with <paramref name="symbol"/> added to its end as a significant member.</returns>
    public ProductionBuilder<T1> Extend<T1>(IGrammarSymbol<T1> symbol)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(symbol);
        return new(_members.Add(symbol), _members.Count, _precedenceToken);
    }

    /// <summary>
    /// Finishes building a production, making it return a value produced by a function.
    /// </summary>
    /// <typeparam name="T">The type of the production's return value.</typeparam>
    /// <param name="fuser">A function that produces the value of the production.</param>
    public IProduction<T> Finish<T>(Func<T> fuser)
    {
        object? fBoxed(ref ParserState state, Span<object?> input) => fuser();
        // We don't use [.. _members] because it generates more IL.
        return new Production<T>(_members.ToImmutableArray(), fBoxed, _precedenceToken);
    }

    /// <summary>
    /// Finishes building a production, making it return a constant value.
    /// </summary>
    /// <typeparam name="T">The type of the production's return value.</typeparam>
    /// <param name="value">The value to return from the production.</param>
    public IProduction<T> FinishConstant<T>(T value)
    {
        object? fBoxed(ref ParserState state, Span<object?> input) => value;
        // We don't use [.. _members] because it generates more IL.
        return new Production<T>(_members.ToImmutableArray(), fBoxed, _precedenceToken);
    }

    /// <inheritdoc/>
    public ProductionBuilder WithPrecedence(object precedenceToken)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(precedenceToken);
        return new(_members, precedenceToken);
    }
}
