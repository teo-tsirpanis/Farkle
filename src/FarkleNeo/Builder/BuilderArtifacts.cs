// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Builder;

/// <summary>
/// Represents the possible artifacts that the builder can produce.
/// </summary>
/// <seealso cref="BuilderResult{T}"/>
[Flags]
public enum BuilderArtifacts {
    /// <summary>
    /// Nothing gets built.
    /// </summary>
    None = 0,
    /// <summary>
    /// Builds a <see cref="Grammar"/> with no state machines.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.Grammar"/>
    GrammarSummary = 1,
    /// <summary>
    /// Builds a <see cref="Grammar"/> with an LR state machine.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.Grammar"/>
    GrammarLrStateMachine = 2,
    /// <summary>
    /// Builds a <see cref="Grammar"/> with a DFA on <see cref="char"/>.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.Grammar"/>
    GrammarDfaOnChar = 4,
    /// <summary>
    /// Builds a <see cref="Tokenizer{Char}"/> on <see cref="char"/>.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.TokenizerOnChar"/>
    TokenizerOnChar = 8,
    /// <summary>
    /// Builds an <see cref="ISemanticProvider{Char, T}"/> on <see cref="char"/>.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.SemanticProviderOnChar"/>
    // We could also add TokenSemanticProviderOnChar and ProductionSemanticProvider because the latter
    // is character-agnostic but that seems very excessive.
    SemanticProviderOnChar = 16,
    /// <summary>
    /// Builds a <see cref="CharParser{T}"/>.
    /// </summary>
    /// <seealso cref="BuilderResult{T}.CharParser"/>
    CharParser = 32
}
