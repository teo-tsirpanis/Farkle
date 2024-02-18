// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides a uniform API surface for operations common to all production builders.
/// </summary>
/// <typeparam name="TSelf">The type that implements the interface.</typeparam>
/// <remarks>
/// This interface cannot be implemented by user code.
/// </remarks>
public interface IProductionBuilder<TSelf> where TSelf : IProductionBuilder<TSelf>
{
    /// <summary>
    /// Dummy internal method to prevent implementing this interface by user code.
    /// </summary>
    internal void MustNotImplement();
    /// <summary>
    /// Appends an <see cref="IGrammarSymbol"/> to the production.
    /// </summary>
    /// <param name="symbol">The symbol to append.</param>
    /// <returns>A production builder with <paramref name="symbol"/> added
    /// to the production's end.</returns>
    TSelf Append(IGrammarSymbol symbol);

    /// <summary>
    /// Changes the precedence token of the production.
    /// </summary>
    /// <param name="precedenceToken">An object that represents the
    /// production in associativity groups.</param>
    /// <returns>A production builder with the precedence token changed to
    /// <paramref name="precedenceToken"/>.</returns>
    TSelf WithPrecedence(object precedenceToken);
}
