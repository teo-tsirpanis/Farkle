// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides factory methods to define terminals.
/// </summary>
public partial class Terminal
{
    /// <summary>
    /// A special terminal that matches a new line.
    /// </summary>
    /// <remarks>
    /// This is different and better than a literal of newline characters.
    /// If used anywhere in the grammar, it indicates that the grammar is
    /// line-based, which means that newline characters are not noise.
    /// Newline characters are are considered the character sequences
    /// <c>\r</c>, <c>\n</c>, or <c>\r\n</c>.
    /// </remarks>
    public static IGrammarSymbol NewLine => Builder.NewLine.Instance;

    /// <summary>
    /// Creates a terminal that does not produce a value.
    /// </summary>
    /// <param name="name">The name of the terminal.</param>
    /// <param name="regex">The <see cref="Regex"/> that matches the terminal.</param>
    public static IGrammarSymbol Create(string name, Regex regex)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(regex);
        return new Terminal(name, regex, Builder.Transformer.GetIdentity<char, object>());
    }

    /// <summary>
    /// Creates a terminal that produces a value.
    /// </summary>
    /// <typeparam name="T">The type of values the terminal will produce.</typeparam>
    /// <param name="name">The name of the terminal.</param>
    /// <param name="regex">The <see cref="Regex"/> that matches the terminal.</param>
    /// <param name="transformer">The transformer to apply to the content of the terminal.</param>
    public static IGrammarSymbol<T> Create<T>(string name, Regex regex, Transformer<char, T> transformer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(regex);
        ArgumentNullExceptionCompat.ThrowIfNull(transformer);
        return new Terminal<T>(name, regex, Builder.Transformer.Box(transformer));
    }

    /// <summary>
    /// Creates a terminal that matches a literal string.
    /// </summary>
    /// <param name="value">The string matched by the terminal.</param>
    /// <remarks>
    /// Multiple instances of literals with the same <paramref name="value"/> resolve to the
    /// same terminal in a grammar, and will not cause conflicts when building it.
    /// </remarks>
    public static IGrammarSymbol Literal(string value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);
        return new Literal(value);
    }

    /// <summary>
    /// Creates a terminal that is never produced by Farkle's default tokenizer.
    /// Users will have to provide a custom tokenizer to match this terminal.
    /// </summary>
    /// <param name="name">The name of the virtual terminal.</param>
    public static IGrammarSymbol Virtual(string name)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        return new VirtualTerminal(name);
    }
}
