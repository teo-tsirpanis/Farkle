// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Provides an extensibility point for <see cref="IParser{TChar, T}"/>s to create
/// custom <see cref="ParserStateContext{TChar, T}"/>s.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
/// <remarks>
/// This is a service interface meant to be returned from <see cref="IServiceProvider.GetService"/>.
/// User code does not need to bother with it; it is automatically considered by
/// <see cref="ParserStateContext.Create"/>.
/// </remarks>
public interface IParserStateContextFactory<TChar, T>
{
    /// <inheritdoc cref="ParserStateContext{TChar, T}.ParserStateContext"/>
    public ParserStateContext<TChar, T> CreateContext(ParserStateContextOptions? options = null);
}
