// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Diagnostics;

/// <summary>
/// Supplies information about the parser's state at the time of an error.
/// </summary>
/// <remarks>
/// This interface should be implemented by error objects that are returned by the tokenizer.
/// </remarks>
public interface IParserStateInfoSupplier
{
    /// <summary>
    /// Enriches the error with information about the parser's state.
    /// </summary>
    /// <param name="expectedTokenNames">The names of the tokens that were expected at the
    /// time of the error. <see langword="null"/> corresponds to the end of file.</param>
    /// <param name="parserState">The number of the parser's state at the time of the error.</param>
    /// <returns>An error object that contains <paramref name="expectedTokenNames"/>
    /// and <paramref name="parserState"/>.</returns>
    /// <remarks>
    /// Farkle's default parser will call this method if the tokenizer returns an error whose object
    /// either implements <see cref="IParserStateInfoSupplier"/>, or is of type <see cref="ParserDiagnostic"/>
    /// and its <see cref="ParserDiagnostic.Message"/> implements <see cref="IParserStateInfoSupplier"/>.
    /// </remarks>
    object WithParserStateInfo(ImmutableArray<string?> expectedTokenNames, int parserState);
}
