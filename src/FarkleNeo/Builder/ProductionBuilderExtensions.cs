// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Farkle.Builder.ProductionBuilders;

namespace Farkle.Builder;

/// <summary>
/// Provides extension methods to manipulate and create production builders and productions.
/// </summary>
public static class ProductionBuilderExtensions
{
    /// <summary>
    /// Appends a literal to the production.
    /// </summary>
    /// <typeparam name="T">The type of the production builder.</typeparam>
    /// <param name="builder">The production builder.</param>
    /// <param name="literal">The literal to append.</param>
    /// <returns>A production builder with <paramref name="literal"/> added
    /// to the production's end.</returns>
    /// <seealso cref="Terminal.Literal"/>
    public static T Append<T>(this T builder, string literal) where T : IProductionBuilder<T>
    {
        ArgumentNullExceptionCompat.ThrowIfNull(literal);
        return builder.Append(Terminal.Literal(literal));
    }

    /// <summary>
    /// Changes the precedence token of the production. This method provides an
    /// API more friendly to C#.
    /// </summary>
    /// <typeparam name="T">The type of the production builder.</typeparam>
    /// <param name="builder">The production builder.</param>
    /// <param name="precedenceToken">A reference where the precedence token will be written to.</param>
    /// <returns></returns>
    /// <seealso cref="IProductionBuilder{T}.WithPrecedence"/>
    public static T WithPrecedence<T>(this T builder, out object precedenceToken) where T : IProductionBuilder<T>
    {
        precedenceToken = new object();
        return builder.WithPrecedence(precedenceToken);
    }

    /// <summary>
    /// Starts a production builder with a literal.
    /// </summary>
    /// <param name="literal">The literal to start the production with.</param>
    public static ProductionBuilder Appended(this string literal) =>
        ProductionBuilder.Empty.Append(literal);

    /// <summary>
    /// Starts a production builder with a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to start the production with.</param>
    public static ProductionBuilder Appended(this IGrammarSymbol symbol) =>
        ProductionBuilder.Empty.Append(symbol);

    /// <summary>
    /// Starts a production builder with a symbol as a significant member.
    /// </summary>
    /// <typeparam name="T1">The type of values the symbol will produce.</typeparam>
    /// <param name="symbol">The symbol to start the production with.</param>
    public static ProductionBuilder<T1> Extended<T1>(this IGrammarSymbol<T1> symbol) =>
        ProductionBuilder.Empty.Extend(symbol);

    /// <summary>
    /// Creates a production made of a literal, that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="literal">The literal to convert.</param>
    /// <param name="fuser">A function that produces the value of the production.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Appended(string)"/> and
    /// <see cref="ProductionBuilder.Finish"/>.
    /// </remarks>
    public static IProduction<T> Finish<T>(this string literal, Func<T> fuser) =>
        literal.Appended().Finish(fuser);

    /// <summary>
    /// Creates a production made of a symbol, that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="symbol">The symbol comprising the production.</param>
    /// <param name="fuser">The function to call when this production is reduced.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Appended(string)"/> and
    /// <see cref="ProductionBuilder.Finish"/>.
    /// </remarks>
    public static IProduction<T> Finish<T>(this IGrammarSymbol symbol, Func<T> fuser) =>
        symbol.Appended().Finish(fuser);

    /// <summary>
    /// Creates a production made of a symbol, that produces a value transformed
    /// from the symbol's value.
    /// </summary>
    /// <typeparam name="T1">The type of values the symbol will produce.</typeparam>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="symbol">The symbol comprising the production.</param>
    /// <param name="fuser">The function to call when this production is reduced.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Extended"/> and
    /// <see cref="ProductionBuilder{T1}.Finish"/>.
    /// </remarks>
    public static IProduction<T> Finish<T1, T>(this IGrammarSymbol<T1> symbol, Func<T1, T> fuser) =>
        symbol.Extended().Finish(fuser);

    /// <summary>
    /// Creates a production made of a literal, that produces a constant value.
    /// </summary>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="literal">The literal comprising the production.</param>
    /// <param name="value">The value the production will produce.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Appended(string)"/> and
    /// <see cref="ProductionBuilder.FinishConstant"/>.
    /// </remarks>
    public static IProduction<T> FinishConstant<T>(this string literal, T value) =>
        literal.Appended().FinishConstant(value);

    /// <summary>
    /// Creates a production made of a symbol, that produces a constant value.
    /// </summary>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="symbol">The symbol comprising the production.</param>
    /// <param name="value">The value the production will produce.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Appended(IGrammarSymbol)"/> and
    /// <see cref="ProductionBuilder.FinishConstant"/>.
    /// </remarks>
    public static IProduction<T> FinishConstant<T>(this IGrammarSymbol symbol, T value) =>
        symbol.Appended().FinishConstant(value);

    /// <summary>
    /// Creates a production made of a symbol, that produces the same value as the symbol.
    /// </summary>
    /// <typeparam name="T">The type of values the production will produce.</typeparam>
    /// <param name="symbol">The symbol comprising the production.</param>
    /// <remarks>
    /// This method is a shortcut that combines <see cref="Appended(IGrammarSymbol)"/> and
    /// <see cref="ProductionBuilder{T1}.AsIs"/>.
    /// </remarks>
    public static IProduction<T> AsProduction<T>(this IGrammarSymbol<T> symbol) =>
        symbol.Extended().AsProduction();

    /// <summary>
    /// Obsolete, use <see cref="AsProduction"/> instead.
    /// </summary>
    [Obsolete(Obsoletions.AsIsApiMessage
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.AsIsApiCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    ), EditorBrowsable(EditorBrowsableState.Never)]
    public static IProduction<T> AsIs<T>(this IGrammarSymbol<T> symbol) =>
        symbol.AsProduction();
}
