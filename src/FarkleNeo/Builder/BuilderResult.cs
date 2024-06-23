// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Builder;

/// <summary>
/// Represents the result of a builder operation.
/// </summary>
/// <typeparam name="T">The type of objects the parser will produce in case of success.</typeparam>
/// <remarks>
/// All properties of this class are nullable and they are populated based on the <see cref="BuilderArtifacts"/>
/// that were requested when building.
/// </remarks>
/// <seealso cref="GrammarBuilderExtensions.Build{T}(IGrammarBuilder{T}, BuilderArtifacts, BuilderOptions?)"/>
/// <seealso cref="GrammarBuilderExtensions.BuildSyntaxCheck(IGrammarBuilder, BuilderArtifacts, BuilderOptions?)"/>
/// <seealso cref="GrammarBuilderExtensions.BuildSyntaxCheck{T}(IGrammarBuilder, BuilderArtifacts, BuilderOptions?)"/>
public sealed class BuilderResult<T>
{
    internal BuilderResult() { }

    internal CharParser<T> GetCharParserOrThrow() =>
        CharParser ?? throw new InvalidOperationException("The CharParser is not available.");

    /// <summary>
    /// The built <see cref="Grammar"/>.
    /// </summary>
    public Grammar? Grammar { get; internal init; }
    /// <summary>
    /// The built <see cref="ISemanticProvider{TChar, T}"/> on <see cref="char"/>.
    /// </summary>
    public ISemanticProvider<char, T>? SemanticProviderOnChar { get; internal init; }
    /// <summary>
    /// The built <see cref="Tokenizer{TChar}"/> on <see cref="char"/>.
    /// </summary>
    public Tokenizer<char>? TokenizerOnChar { get; internal init; }
    /// <summary>
    /// The built <see cref="CharParser{T}"/>.
    /// </summary>
    public CharParser<T>? CharParser { get; internal init; }
}
