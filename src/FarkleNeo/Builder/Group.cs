// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides factory methods to define lexical groups.
/// </summary>
/// <seealso cref="Grammars.Group"/>
public partial class Group
{
    /// <summary>
    /// Creates a line group that does not produce a value.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="start">The sequence of characters that start the group.</param>
    public static IGrammarSymbol Line(string name, string start)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(start);
        return new LineGroup(name, start, Builder.Transformer.GetIdentity<char, object>());
    }

    /// <summary>
    /// Creates a line group that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the group will produce.</typeparam>
    /// <param name="name">The name of the group.</param>
    /// <param name="start">The sequence of characters that start the group.</param>
    /// <param name="transformer">The transformer to apply to the content of the group.</param>
    public static IGrammarSymbol<T> Line<T>(string name, string start, Transformer<char, T> transformer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(start);
        ArgumentNullExceptionCompat.ThrowIfNull(transformer);
        return new LineGroup<T>(name, start, Builder.Transformer.Box(transformer));
    }

    /// <summary>
    /// Creates a block group that does not produce a value.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="start">The sequence of characters that start the group.</param>
    /// <param name="end">The sequence of characters that end the group.</param>
    public static IGrammarSymbol Block(string name, string start, string end)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(start);
        ArgumentNullExceptionCompat.ThrowIfNull(end);
        return new BlockGroup(name, start, end, Builder.Transformer.GetIdentity<char, object>());
    }

    /// <summary>
    /// Creates a block group that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the group will produce.</typeparam>
    /// <param name="name">The name of the group.</param>
    /// <param name="start">The sequence of characters that start the group.</param>
    /// <param name="end">The sequence of characters that end the group.</param>
    /// <param name="transformer">The transformer to apply to the content of the group.</param>
    public static IGrammarSymbol<T> Block<T>(string name, string start, string end, Transformer<char, T> transformer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(start);
        ArgumentNullExceptionCompat.ThrowIfNull(end);
        ArgumentNullExceptionCompat.ThrowIfNull(transformer);
        return new BlockGroup<T>(name, start, end, Builder.Transformer.Box(transformer));
    }
}
