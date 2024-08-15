// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Internal characteristics of a parsing operation.
/// </summary>
/// <seealso cref="ParserState.Attributes"/>
[Flags]
internal enum ParserStateAttributes : byte
{
    /// <summary>
    /// No attributes are defined.
    /// </summary>
    None = 0,
    /// <inheritdoc cref="ParserState.TokenizerSupportsSuspending"/>
    TokenizerSupportsSuspending = 1,
    /// <summary>
    /// The inverse of <see cref="ParserState.IsSingleTokenizerInChain"/>.
    /// </summary>
    /// <remarks>
    /// The default state is that there is only one tokenizer in the chain, which is why
    /// this attribute is the inverse of the public API.
    /// </remarks>
    HasMoreThanOneTokenizerInChain = 2
}
