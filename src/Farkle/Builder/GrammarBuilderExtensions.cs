// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
    internal static ref readonly GrammarGlobalOptions GetOptions(this IGrammarBuilder builder)
    {
        // This implicitly checks the builder for null in all extension methods.
        ArgumentNullException.ThrowIfNull(builder);
        return ref builder is GrammarBuilderWrapper wrapper ? ref wrapper.Options : ref GrammarGlobalOptions.Default;
    }

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

    internal static string GetGrammarName(this IGrammarBuilder builder) =>
        builder.GetOptions().GrammarName ?? builder.Symbol.Name;

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
        ArgumentNullException.ThrowIfNull(builder);
        if (builder is IGrammarBuilder<object?> b)
        {
            return b;
        }
        return new GrammarBuilderWrapper<object>(in builder.GetOptions(), builder.Symbol);
    }

    /// <summary>
    /// Changes the case sensitivity option of a grammar. This overload accepts a
    /// <see cref="CaseSensitivity"/> value for more flexibility.
    /// </summary>
    /// <param name="builder">The grammar builder.</param>
    /// <param name="value">The case sensitivity option for the grammar.</param>
    public static IGrammarBuilder CaseSensitive(this IGrammarBuilder builder, CaseSensitivity value)
    {
        ArgumentNullException.ThrowIfNull(builder);
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
        ArgumentNullException.ThrowIfNull(builder);
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
    /// <seealso cref="GrammarInfo.Name"/>
    public static IGrammarBuilder WithGrammarName(this IGrammarBuilder builder, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value == builder.GetOptions().GrammarName
            ? builder
            : builder.WithOptions(builder.GetOptions() with { GrammarName = value });
    }

    /// <inheritdoc cref="WithGrammarName"/>
    public static IGrammarBuilder<T> WithGrammarName<T>(this IGrammarBuilder<T> builder, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

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
        ArgumentNullException.ThrowIfNull(value);

        return value == builder.GetOptions().OperatorScope
            ? builder
            : builder.WithOptions(builder.GetOptions() with { OperatorScope = value });
    }

    /// <inheritdoc cref="WithOperatorScope{T}(IGrammarBuilder{T}, OperatorScope)"/>
    public static IGrammarBuilder<T> WithOperatorScope<T>(this IGrammarBuilder<T> builder, OperatorScope value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value == builder.GetOptions().OperatorScope
            ? builder
            : builder.WithOptions(builder.GetOptions() with { OperatorScope = value });
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
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(regex);

        return builder.WithOptions(builder.GetOptions().AddNoiseSymbol(name, regex));
    }

    /// <inheritdoc cref="AddNoiseSymbol"/>
    public static IGrammarBuilder<T> AddNoiseSymbol<T>(this IGrammarBuilder<T> builder, string name, Regex regex)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(regex);

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
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        return builder.WithOptions(builder.GetOptions().AddBlockComment(start, end));
    }

    /// <inheritdoc cref="AddBlockComment"/>
    public static IGrammarBuilder<T> AddBlockComment<T>(this IGrammarBuilder<T> builder, string start, string end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

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
        ArgumentNullException.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddLineComment(start));
    }

    /// <inheritdoc cref="AddLineComment"/>
    public static IGrammarBuilder<T> AddLineComment<T>(this IGrammarBuilder<T> builder, string start)
    {
        ArgumentNullException.ThrowIfNull(start);

        return builder.WithOptions(builder.GetOptions().AddLineComment(start));
    }

    /// <summary>
    /// Obsolete. Upgrade your code to use the new precompiler APIs. See
    /// <see href="https://farkle.dev/migration/60-70.html?tabs=csharp#changes-to-the-precompiler"/> for further guidance.
    /// </summary>
    /// <seealso cref="PrecompilerInputAttribute"/>
    /// <seealso cref="PrecompilerOutputAttribute"/>
    [Obsolete("Upgrade your code to use the new precompiler API. See https://farkle.dev/migration/60-70.html?tabs=csharp#changes-to-the-precompiler for further guidance.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never), ExcludeFromCodeCoverage]
    public static IGrammarBuilder MarkForPrecompile(this IGrammarBuilder builder, Assembly? asm = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = asm;
        return builder;
    }

    /// <inheritdoc cref="MarkForPrecompile(IGrammarBuilder, Assembly?)"/>
    [Obsolete("Upgrade your code to use the new precompiler API. See https://farkle.dev/migration/60-70.html?tabs=csharp#changes-to-the-precompiler for further guidance.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never), ExcludeFromCodeCoverage]
    public static IGrammarBuilder<T> MarkForPrecompile<T>(this IGrammarBuilder<T> builder, Assembly? asm = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = asm;
        return builder;
    }

    /// <summary>
    /// Builds an <see cref="IGrammarBuilder"/>. This is the entry point to Farkle's builder.
    /// </summary>
    /// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="outputs">The set of outputs to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <param name="isSyntaxCheck">Whether to use a dummy semantic provider instead of building one.</param>
    private static BuilderResult<T> BuildImpl<T>(this IGrammarBuilder builder, BuilderOutputs outputs,
        BuilderOptions? options = null, bool isSyntaxCheck = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        options ??= BuilderOptions.Default;

        // Add dependencies between outputs.
        // The order is important; if an output appears in the first parameter,
        // it cannot appear in the second parameter of a subsequent call.
        AddOutputDependencies(BuilderOutputs.CharParser,
            BuilderOutputs.SemanticProviderOnChar | BuilderOutputs.TokenizerOnChar | BuilderOutputs.GrammarLrStateMachine);
        AddOutputDependencies(BuilderOutputs.TokenizerOnChar,
            BuilderOutputs.GrammarDfaOnChar);
        AddOutputDependencies(BuilderOutputs.GrammarLrStateMachine | BuilderOutputs.GrammarDfaOnChar,
            BuilderOutputs.GrammarSummary);

        Grammar? grammar = null;
        ISemanticProvider<char, T>? semanticProvider = null;
        Tokenizer<char>? tokenizer = null;
        CharParser<T>? parser = null;

        if (outputs != BuilderOutputs.None)
        {
            GrammarDefinition grammarDefinition = GrammarDefinition.Create(builder, options.Log, options.CancellationToken);

            List<BuilderDiagnostic>? errors = null;
            // We will collect errors only if we need to report them from a failing parser or tokenizer.
            if ((outputs & (BuilderOutputs.TokenizerOnChar | BuilderOutputs.CharParser)) != 0)
            {
                errors = [];
            }

            if ((outputs & BuilderOutputs.GrammarSummary) != 0)
            {
                grammar = GrammarBuild.Build(grammarDefinition, outputs, options, errors);
            }

            object? customError = errors is null or [] ? null : new CompositeDiagnostic<BuilderDiagnostic>(errors);

            if ((outputs & BuilderOutputs.TokenizerOnChar) != 0)
            {
                // Custom error is the same for both the parser and the tokenizer, which can
                // give confusing messages when a failing tokenizer gets swapped with a
                // working one. We can fix this by providing a separate custom error for the
                // tokenizer.
                tokenizer = Tokenizer.Create(grammar!, false, customError);
            }

            if ((outputs & BuilderOutputs.SemanticProviderOnChar) != 0)
            {
                semanticProvider = isSyntaxCheck
                    ? SyntaxChecker<char, T>.Instance!
                    : SemanticProviderBuild.Build<T>(grammarDefinition);
            }

            if ((outputs & BuilderOutputs.CharParser) != 0)
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

        // Adds dependencies between outputs. If one of dependents is specified, dependencies will be built as well.
        void AddOutputDependencies(BuilderOutputs dependents, BuilderOutputs dependencies)
        {
            if ((outputs & dependents) != 0)
            {
                outputs |= dependencies;
            }
        }
    }

    /// <summary>
    /// Builds multiple outputs from the given <see cref="IGrammarBuilder{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="outputs">The set of outputs to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <returns>
    /// A <see cref="BuilderResult{T}"/> object with the properties of the requested outputs populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The builder will reuse resources to build the requested outputs where applicable.
    /// </para>
    /// <para>
    /// Additional outputs may be built beyond the ones requested, if they are dependencies of the requested
    /// outputs. For example, if <see cref="BuilderOutputs.CharParser"/> is requested, the builder will also
    /// build <see cref="BuilderOutputs.TokenizerOnChar"/>, <see cref="BuilderOutputs.SemanticProviderOnChar"/>.
    /// </para>
    /// </remarks>
    public static BuilderResult<T> Build<T>(this IGrammarBuilder<T> builder, BuilderOutputs outputs, BuilderOptions? options = null) =>
        builder.BuildImpl<T>(outputs, options);

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
        builder.Build(BuilderOutputs.CharParser, options).GetCharParserOrThrow();

    /// <summary>
    /// Builds multiple outputs from the given untyped <see cref="IGrammarBuilder"/>.
    /// </summary>
    /// <typeparam name="T">The supposed return type of the parser and the semantic provider. Must be a reference type.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <param name="outputs">The set of outputs to build.</param>
    /// <param name="options">Used to customize the building process. Optional.</param>
    /// <returns>
    /// A <see cref="BuilderResult{T}"/> object with the properties of the requested outputs populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The builder will reuse resources to build the requested outputs where applicable.
    /// </para>
    /// <para>
    /// Additional outputs may be built beyond the ones requested, if they are dependencies of the requested
    /// outputs. For example, if <see cref="BuilderOutputs.CharParser"/> is requested, the builder will also
    /// build <see cref="BuilderOutputs.TokenizerOnChar"/>, <see cref="BuilderOutputs.SemanticProviderOnChar"/>.
    /// </para>
    /// <para>
    /// If requested, the builder will create a syntax-checking parser and semantic provider that will not execute
    /// any semantic actions and produce <see langword="null"/> semantic values on success.
    /// </para>
    /// </remarks>
    public static BuilderResult<T?> BuildSyntaxCheck<T>(this IGrammarBuilder builder, BuilderOutputs outputs, BuilderOptions? options = null) where T : class? =>
        builder.BuildImpl<T?>(outputs, options, isSyntaxCheck: true);

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
        builder.BuildSyntaxCheck<T>(BuilderOutputs.CharParser, options).GetCharParserOrThrow();

    /// <inheritdoc cref="BuildSyntaxCheck{T}(IGrammarBuilder, BuilderOptions?)"/>
    public static CharParser<object?> BuildSyntaxCheck(this IGrammarBuilder builder, BuilderOptions? options = null) =>
        builder.BuildSyntaxCheck<object>(options);

    /// <inheritdoc cref="BuildSyntaxCheck{T}(IGrammarBuilder, BuilderOutputs, BuilderOptions?)"/>
    public static BuilderResult<object?> BuildSyntaxCheck(this IGrammarBuilder builder, BuilderOutputs outputs, BuilderOptions? options = null) =>
        builder.BuildSyntaxCheck<object>(outputs, options);

    /// <summary>
    /// Obsolete. Use <see cref="BuildSyntaxCheck(IGrammarBuilder, BuilderOptions?)"/> instead.
    /// </summary>
    [Obsolete(Obsoletions.BuildUntypedMessage, DiagnosticId = Obsoletions.BuildUntypedCode, UrlFormat = Obsoletions.SharedUrlFormat)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CharParser<object?> BuildUntyped(this IGrammarBuilder builder) =>
        builder.BuildSyntaxCheck();

    /// <summary>
    /// Creates an <see cref="ISemanticProvider{TChar, T}"/> for the given <see cref="IGrammarBuilder{T}"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the semantic provider.</typeparam>
    /// <param name="builder">The grammar to build.</param>
    /// <remarks>
    /// By not building a whole grammar, some expensive steps are skipped, and
    /// by using this function instead of <see cref="Build{T}(IGrammarBuilder{T}, BuilderOutputs, BuilderOptions?)"/>,
    /// most of the grammar building code can be trimmed away. This function is
    /// useful only in some very limited scenarios, such as having many grammar
    /// builders with an identical grammar but different semantic providers.
    /// </remarks>
    /// <seealso cref="CharParser{T}.WithSemanticProvider{TNew}(ISemanticProvider{char, TNew})"/>
    public static ISemanticProvider<char, T> BuildSemanticProvider<T>(this IGrammarBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Building a semantic provider does not produce meaningful diagnostics,
        // and does not take more than linear time. Therefore we don't have to
        // accept GrammarOptions for logging and cancellation.
        var grammarDefinition = GrammarDefinition.Create(builder);
        return SemanticProviderBuild.Build<T>(grammarDefinition);
    }
}
