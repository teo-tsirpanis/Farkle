// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;

namespace Farkle.Builder;

/// <summary>
/// Contains extension methods that to set configuration options on <see cref="IGrammarBuilder"/>
/// and <see cref="IGrammarBuilder{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These methods do not modify the object they are called on. Instead, they return a new object
/// with the requested configuration option changed.
/// </para>
/// <para>
/// Because these methods apply to the entire grammar, they must be called on the topmost symbol
/// of the grammar, and after the methods on <see cref="GrammarSymbolConfigurationExtensions"/>. Failure to do
/// so will result in compile errors.
/// </para>
/// </remarks>
public static class GrammarBuilderConfigurationExtensions
{
    internal static ref readonly GrammarGlobalOptions GetOptions(this IGrammarBuilder builder) =>
        ref builder is GrammarBuilderWrapper wrapper ? ref wrapper.Options : ref GrammarGlobalOptions.Default;

    private static IGrammarBuilder WithOptions(this IGrammarBuilder builder, in GrammarGlobalOptions options)
    {
        Debug.Assert(builder is GrammarBuilderWrapper or IGrammarSymbol);

        return builder is GrammarBuilderWrapper wrapper
            ? wrapper.WithOptions(in options)
            : new GrammarBuilderWrapper(in options, (IGrammarSymbol)builder);
    }

    private static IGrammarBuilder<T> WithOptions<T>(this IGrammarBuilder<T> builder, in GrammarGlobalOptions options)
    {
        Debug.Assert(builder is GrammarBuilderWrapper<T> or IGrammarSymbol<T>);

        return builder is GrammarBuilderWrapper<T> wrapper
            ? wrapper.WithOptions(in options)
            : new GrammarBuilderWrapper<T>(in options, (IGrammarSymbol<T>)builder);
    }

    /// <summary>
    /// Changes the case sensitivity option of a grammar. This overload accepts a
    /// <see cref="CaseSensitivity"/> value for more flexibility.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">The case sensitivity option for the grammar.</param>
    public static IGrammarBuilder CaseSensitive(this IGrammarBuilder builder, CaseSensitivity value)
    {
        if (value < CaseSensitivity.CaseSensitive || value > CaseSensitivity.CaseInsensitive)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(value));
        }

        return value == builder.GetOptions().CaseSensitivity
            ? builder
            : builder.WithOptions(builder.GetOptions() with { CaseSensitivity = value });
    }

    /// <inheritdoc cref="CaseSensitive(IGrammarBuilder, CaseSensitivity)"/>
    public static IGrammarBuilder<T> CaseSensitive<T>(this IGrammarBuilder<T> builder, CaseSensitivity value)
    {
        if (value < CaseSensitivity.CaseSensitive || value > CaseSensitivity.CaseInsensitive)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(value));
        }

        return value == builder.GetOptions().CaseSensitivity
            ? builder
            : builder.WithOptions(builder.GetOptions() with { CaseSensitivity = value });
    }

    /// <summary>
    /// Changes the case sensitivity option of a grammar. This overload accepts a
    /// <see cref="bool"/> value for convenience and compatibility.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">Whether the grammar will be case sensitive or not.</param>
    public static IGrammarBuilder CaseSensitive(this IGrammarBuilder builder, bool value) =>
        builder.CaseSensitive(value ? CaseSensitivity.CaseSensitive : CaseSensitivity.CaseInsensitive);

    /// <inheritdoc cref="CaseSensitive(IGrammarBuilder, bool)"/>
    public static IGrammarBuilder<T> CaseSensitive<T>(this IGrammarBuilder<T> builder, bool value) =>
        builder.CaseSensitive(value ? CaseSensitivity.CaseSensitive : CaseSensitivity.CaseInsensitive);

    /// <summary>
    /// Changes whether whitespace is automatically ignored in the grammar.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">Whether to automatically ignore whitespace in the grammar.</param>
    /// <remarks>
    /// This option is set to <see langword="true"/> by default. Whitespace is defined as the
    /// characters <c>'\t'</c>, <c>'\n'</c>, <c>'\r'</c>, and <c>' '</c>.
    /// </remarks>
    public static IGrammarBuilder AutoWhitespace(this IGrammarBuilder builder, bool value) =>
        value == builder.GetOptions().AutoWhitespace
            ? builder
            : builder.WithOptions(builder.GetOptions() with { AutoWhitespace = value });

    /// <inheritdoc cref="AutoWhitespace"/>
    public static IGrammarBuilder<T> AutoWhitespace<T>(this IGrammarBuilder<T> builder, bool value) =>
        value == builder.GetOptions().AutoWhitespace
            ? builder
            : builder.WithOptions(builder.GetOptions() with { AutoWhitespace = value });

    /// <summary>
    /// Changes whether to ignore unexpected occurrences of <see cref="Terminal.NewLine"/> symbols in
    /// the grammar.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">Whether to ignore unexpected new lines in the grammar.</param>
    /// <remarks>
    /// <para>
    /// In versions of Farkle prior to 7 this option did not exist and the behavior was always
    /// equivalent to <see langword="false"/>. Since Farkle 7 the option's default value was changed
    /// to be equal to the option set in <see cref="AutoWhitespace"/>. The reason for this change is
    /// that the previous behavior was unintuitive and rarely useful.
    /// </para>
    /// <para>
    /// If the grammar does not contain a <see cref="Terminal.NewLine"/> symbol, this option has no
    /// effect.
    /// </para>
    /// </remarks>
    public static IGrammarBuilder NewLineIsNoisy(this IGrammarBuilder builder, bool value) =>
        value == builder.GetOptions().NewLineIsNoisy
            ? builder
            : builder.WithOptions(builder.GetOptions() with { NewLineIsNoisy = value });

    /// <inheritdoc cref="AutoWhitespace"/>
    public static IGrammarBuilder<T> NewLineIsNoisy<T>(this IGrammarBuilder<T> builder, bool value) =>
        value == builder.GetOptions().NewLineIsNoisy
            ? builder
            : builder.WithOptions(builder.GetOptions() with { NewLineIsNoisy = value });

    /// <summary>
    /// Changes the name of the grammar.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">The new name of the grammar.</param>
    /// <remarks>
    /// This value is used only for diagnostic and documentation purposes. Its default value is equal to
    /// the <see cref="IGrammarSymbol.Name"/> of the grammar's start symbol.
    /// </remarks>
    /// <seealso cref="IGrammarSymbol.Name"/>
    /// <seealso cref="Grammars.GrammarInfo.Name"/>
    /// <seealso cref="GrammarSymbolConfigurationExtensions.Rename"/>
    public static IGrammarBuilder WithGrammarName(this IGrammarBuilder builder, string value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return value == builder.GetOptions().GrammarName
            ? builder
            : builder.WithOptions(builder.GetOptions() with { GrammarName = value });
    }

    /// <inheritdoc cref="WithGrammarName"/>
    public static IGrammarBuilder<T> WithGrammarName<T>(this IGrammarBuilder<T> builder, string value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return value == builder.GetOptions().GrammarName
            ? builder
            : builder.WithOptions(builder.GetOptions() with { GrammarName = value });
    }

    /// <summary>
    /// Changes the <see cref="OperatorScope"/> used to resolve parser conflicts in the grammar.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">The <see cref="OperatorScope"/> to use in the grammar.</param>
    /// <remarks>
    /// In versions of Farkle prior to 7 this option could be applied to individual symbols and
    /// still had effect on the entire grammar. Since Farkle 7 a grammar may only have one operator
    /// scope. The reason for this change is that the previous behavior had limited utility and lots
    /// of edge cases that were difficult to define and handle.
    /// </remarks>
    public static IGrammarBuilder WithOperatorScope(this IGrammarBuilder builder, OperatorScope value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return value == builder.GetOptions().OperatorScope
            ? builder
            : builder.WithOptions(builder.GetOptions() with { OperatorScope = value });
    }

    /// <inheritdoc cref="WithOperatorScope"/>
    public static IGrammarBuilder<T> WithOperatorScope<T>(this IGrammarBuilder<T> builder, OperatorScope value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return value == builder.GetOptions().OperatorScope
            ? builder
            : builder.WithOptions(builder.GetOptions() with { OperatorScope = value });
    }

    /// <summary>
    /// Changes the <see cref="CompatibilityLevel"/> that will be used to build the grammar.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">The <see cref="CompatibilityLevel"/> to use in the grammar.</param>
    /// <remarks>
    /// <para>
    /// If this option is not set, the latest available compatibility level (at runtime, or at
    /// compile time if the precompiler is used).
    /// </para>
    /// <para>
    /// Because the precompiler already protects from behavior breaking changes in the builder,
    /// setting a compatibility level for a grammar will have benefits only if the precompiler
    /// is not being used.
    /// </para>
    /// </remarks>
    public static IGrammarBuilder WithCompatibilityLevel(this IGrammarBuilder builder, CompatibilityLevel value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return builder;
    }

    /// <inheritdoc cref="WithCompatibilityLevel"/>
    public static IGrammarBuilder<T> WithCompatibilityLevel<T>(this IGrammarBuilder<T> builder, CompatibilityLevel value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return builder;
    }

    /// <summary>
    /// Adds a noise symbol to the grammar that will be ignored if it is encountered in the input.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="name">The name of the noise symbol. Used for diagnostics and documentation
    /// purposes only.</param>
    /// <param name="regex">The regular expression that matches the noise symbol.</param>
    public static IGrammarBuilder AddNoiseSymbol(this IGrammarBuilder builder, string name, Regex regex)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(regex);

        return builder.WithOptions(builder.GetOptions().AddNoiseSymbol(name, regex));
    }

    /// <inheritdoc cref="AddNoiseSymbol"/>
    public static IGrammarBuilder<T> AddNoiseSymbol<T>(this IGrammarBuilder<T> builder, string name, Regex regex)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(regex);

        return builder.WithOptions(builder.GetOptions().AddNoiseSymbol(name, regex));
    }

    /// <summary>
    /// Adds a comment to the grammar that starts and ends with specific sequences of characters.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="start">The sequence of characters that starts the comment.</param>
    /// <param name="end">The sequence of characters that ends the comment.</param>
    public static IGrammarBuilder AddBlockComment(this IGrammarBuilder builder, string start, string end)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddBlockComment(start, end));
    }

    /// <inheritdoc cref="AddBlockComment"/>
    public static IGrammarBuilder<T> AddBlockComment<T>(this IGrammarBuilder<T> builder, string start, string end)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddBlockComment(start, end));
    }

    /// <summary>
    /// Adds a comment to the grammar that starts with a specific sequence of characters and ends at the
    /// end of a line or the end of the input.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="start">The sequence of characters that starts the comment.</param>
    public static IGrammarBuilder AddLineComment(this IGrammarBuilder builder, string start)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddLineComment(start));
    }

    /// <inheritdoc cref="AddLineComment"/>
    public static IGrammarBuilder<T> AddLineComment<T>(this IGrammarBuilder<T> builder, string start)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddLineComment(start));
    }
}
