// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

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
/// of the grammar, and after the methods on <see cref="GrammarSymbolExtensions"/>. Failure to do
/// so will result in compile errors.
/// </para>
/// </remarks>
public static class GrammarBuilderExtensions
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

    internal static IGrammarSymbol Unwrap(this IGrammarBuilder builder)
    {
        Debug.Assert(builder is IGrammarSymbol or GrammarBuilderWrapper);
        return builder is IGrammarSymbol symbol ? symbol : ((GrammarBuilderWrapper)builder).Symbol;
    }

    /// <summary>
    /// Changes the type of <see cref="IGrammarBuilder"/> to a generic <see cref="IGrammarBuilder{T}"/>
    /// of type <see cref="object"/>, forcing it to return a value.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <returns>An <see cref="IGrammarBuilder{T}"/> that returns the object <paramref name="builder"/>
    /// would return. If <paramref name="builder"/> had been created with the untyped API, the returned
    /// object will be <see langword="null"/>.</returns>
    public static IGrammarBuilder<object?> Cast(this IGrammarBuilder builder)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(builder);
        if (builder is IGrammarBuilder<object?> b)
        {
            return b;
        }
        return new GrammarBuilderWrapper<object>(in builder.GetOptions(), builder.Unwrap());
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
    public static IGrammarBuilder CaseSensitive(this IGrammarBuilder builder, bool value = true) =>
        builder.CaseSensitive(value ? CaseSensitivity.CaseSensitive : CaseSensitivity.CaseInsensitive);

    /// <inheritdoc cref="CaseSensitive(IGrammarBuilder, bool)"/>
    public static IGrammarBuilder<T> CaseSensitive<T>(this IGrammarBuilder<T> builder, bool value = true) =>
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
    /// <seealso cref="GrammarSymbolExtensions.Rename"/>
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
    public static IGrammarBuilder WithOperatorScope(this IGrammarBuilder builder, params OperatorScope value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(value);

        return value == builder.GetOptions().OperatorScope
            ? builder
            : builder.WithOptions(builder.GetOptions() with { OperatorScope = value });
    }

    /// <inheritdoc cref="WithOperatorScope"/>
    public static IGrammarBuilder<T> WithOperatorScope<T>(this IGrammarBuilder<T> builder, params OperatorScope value)
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

    /// <summary>
    /// Builds an <see cref="IGrammarBuilder"/>. This is the entry point to Farkle's builder.
    /// </summary>
    /// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="artifacts">The set of artifacts to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <param name="isSyntaxCheck">Whether to use a dummy semantic provider instead of building one.</param>
    private static BuilderResult<T> BuildImpl<T>(this IGrammarBuilder builder, BuilderArtifacts artifacts,
        BuilderOptions? options = null, bool isSyntaxCheck = false)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(builder);

        options ??= BuilderOptions.Default;

        // Add dependencies between artifacts.
        // The order is important; if an artifact appears in the first parameter,
        // it cannot appear in the second parameter of a subsequent call.
        AddArtifactDependencies(BuilderArtifacts.CharParser,
            BuilderArtifacts.SemanticProviderOnChar | BuilderArtifacts.TokenizerOnChar | BuilderArtifacts.GrammarLrStateMachine);
        AddArtifactDependencies(BuilderArtifacts.TokenizerOnChar,
            BuilderArtifacts.GrammarDfaOnChar);
        AddArtifactDependencies(BuilderArtifacts.GrammarLrStateMachine | BuilderArtifacts.GrammarDfaOnChar,
            BuilderArtifacts.GrammarSummary);

        Grammar? grammar = null;
        ISemanticProvider<char, T>? semanticProvider = null;
        Tokenizer<char>? tokenizer = null;
        CharParser<T>? parser = null;

        if (artifacts != BuilderArtifacts.None)
        {
            GrammarDefinition grammarDefinition = GrammarDefinition.Create(builder, options.Log, options.CancellationToken);

            List<BuilderDiagnostic>? errors = null;
            // We will collect errors only if we need to report them from a failing parser or tokenizer.
            if ((artifacts & BuilderArtifacts.TokenizerOnChar | BuilderArtifacts.CharParser) != 0)
            {
                errors = [];
            }

            if ((artifacts & BuilderArtifacts.GrammarSummary) != 0)
            {
                grammar = GrammarBuild.Build(grammarDefinition, artifacts, options, errors);
            }

            object? customError = errors is null or [] ? null : new CompositeDiagnostic<BuilderDiagnostic>(errors);

            if ((artifacts & BuilderArtifacts.TokenizerOnChar) != 0)
            {
                // Custom error is the same for both the parser and the tokenizer, which can
                // give confusing messages when a failing tokenizer gets swapped with a
                // working one. We can fix this by providing a separate custom error for the
                // tokenizer.
                tokenizer = Tokenizer.Create<char>(grammar!, false, customError);
            }

            if ((artifacts & BuilderArtifacts.SemanticProviderOnChar) != 0)
            {
                semanticProvider = isSyntaxCheck
                    ? SyntaxChecker<char, T>.Instance!
                    : SemanticProviderBuild.Build<T>(grammarDefinition);
            }

            if ((artifacts & BuilderArtifacts.CharParser) != 0)
            {
                parser = CharParser.Create(grammar!, tokenizer!, semanticProvider!, customError);
            }
        }

        return new BuilderResult<T>
        {
            Grammar = grammar,
            CharParser = parser,
            SemanticProviderOnChar = semanticProvider,
            TokenizerOnChar = tokenizer
        };

        // Adds dependencies between artifacts. If one of dependents is specified, dependencies will be built as well.
        void AddArtifactDependencies(BuilderArtifacts dependents, BuilderArtifacts dependencies)
        {
            if ((artifacts & dependents) != 0)
            {
                artifacts |= dependencies;
            }
        }
    }

    /// <summary>
    /// Builds multiple artifacts from the given <see cref="IGrammarBuilder{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="artifacts">The set of artifacts to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <returns>
    /// A <see cref="BuilderResult{T}"/> object with the properties of the requested artifacts populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The builder will reuse resources to build the requested artifacts where applicable.
    /// </para>
    /// <para>
    /// Additional artifacts may be built beyond the ones requested, if they are dependencies of the requested
    /// artifacts. For example, if <see cref="BuilderArtifacts.CharParser"/> is requested, the builder will also
    /// build <see cref="BuilderArtifacts.TokenizerOnChar"/>, <see cref="BuilderArtifacts.SemanticProviderOnChar"/>.
    /// </para>
    /// </remarks>
    public static BuilderResult<T> Build<T>(this IGrammarBuilder<T> builder, BuilderArtifacts artifacts, BuilderOptions? options = null) =>
        builder.BuildImpl<T>(artifacts, options);

    /// <summary>
    /// Creates a <see cref="CharParser{T}"/> from the given <see cref="IGrammarBuilder{T}"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the parser or semantic provider.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <returns>
    /// A <see cref="CharParser{T}"/> object that can be used to parse text.
    /// If building the grammar failed, the parser's <see cref="CharParser{T}.IsFailing"/>
    /// property will be <see langword="true"/>. Detailed error information can be
    /// obtained by trying to parse any text, and casting the result's <see cref="ParserResult{T}.Error"/>
    /// property to <see cref="IReadOnlyList{BuilderDiagnostic}"/> of type <see cref="BuilderDiagnostic"/>.
    /// </returns>
    public static CharParser<T> Build<T>(this IGrammarBuilder<T> builder, BuilderOptions? options = null) =>
        builder.Build(BuilderArtifacts.CharParser, options).GetCharParserOrThrow();

    /// <summary>
    /// Builds multiple artifacts from the given untyped <see cref="IGrammarBuilder"/>.
    /// </summary>
    /// <typeparam name="T">The supposed return type of the parser and the semantic provider. Must be a reference type.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="artifacts">The set of artifacts to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <returns>
    /// A <see cref="BuilderResult{T}"/> object with the properties of the requested artifacts populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The builder will reuse resources to build the requested artifacts where applicable.
    /// </para>
    /// <para>
    /// Additional artifacts may be built beyond the ones requested, if they are dependencies of the requested
    /// artifacts. For example, if <see cref="BuilderArtifacts.CharParser"/> is requested, the builder will also
    /// build <see cref="BuilderArtifacts.TokenizerOnChar"/>, <see cref="BuilderArtifacts.SemanticProviderOnChar"/>.
    /// </para>
    /// <para>
    /// If requested, the builder will create a syntax-checking parser and semantic provider that will not execute
    /// any semantic actions and produce <see langword="null"/> semantic values on success.
    /// </para>
    /// </remarks>
    public static BuilderResult<T?> BuildSyntaxCheck<T>(this IGrammarBuilder builder, BuilderArtifacts artifacts, BuilderOptions? options = null) where T : class? =>
        builder.BuildImpl<T?>(artifacts, options, isSyntaxCheck: true);

    /// <summary>
    /// Creates a syntax-checking <see cref="CharParser{T}"/> from the given <see cref="IGrammarBuilder{T}"/>.
    /// </summary>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <typeparam name="T">The supposed return type of the parser. Must be a reference type.</typeparam>
    /// <returns>
    /// A <see cref="CharParser{T}"/> object that can be used to parse text, and will always return
    /// <see langword="null"/> on success.
    /// </returns>
    /// <remarks>
    /// If building the grammar failed, the parser's <see cref="CharParser{T}.IsFailing"/>
    /// property will be <see langword="true"/>. Detailed error information can be
    /// obtained by trying to parse any text, and casting the result's <see cref="ParserResult{T}.Error"/>
    /// property to <see cref="IReadOnlyList{BuilderDiagnostic}"/> of type <see cref="BuilderDiagnostic"/>.
    /// </remarks>
    public static CharParser<T?> BuildSyntaxCheck<T>(this IGrammarBuilder builder, BuilderOptions? options = null) where T : class? =>
        builder.BuildSyntaxCheck<T>(BuilderArtifacts.CharParser, options).GetCharParserOrThrow();

    /// <inheritdoc cref="BuildSyntaxCheck{T}(IGrammarBuilder, BuilderOptions?)"/>
    public static CharParser<object?> BuildSyntaxCheck(this IGrammarBuilder builder, BuilderOptions? options = null) =>
        builder.BuildSyntaxCheck<object>(options);

    /// <inheritdoc cref="BuildSyntaxCheck{T}(IGrammarBuilder, BuilderArtifacts, BuilderOptions?)"/>
    public static BuilderResult<object?> BuildSyntaxCheck(this IGrammarBuilder builder, BuilderArtifacts artifacts, BuilderOptions? options = null) =>
        builder.BuildSyntaxCheck<object>(artifacts, options);

    /// <summary>
    /// Obsolete. Use <see cref="BuildSyntaxCheck(IGrammarBuilder, BuilderOptions?)"/> instead.
    /// </summary>
    [Obsolete(Obsoletions.BuildUntypedMessage
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.BuildUntypedCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    ), EditorBrowsable(EditorBrowsableState.Never)]
    public static CharParser<object?> BuildUntyped(this IGrammarBuilder builder) =>
        builder.BuildSyntaxCheck();
}
