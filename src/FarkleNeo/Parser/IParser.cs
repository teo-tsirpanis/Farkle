// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Parses a series of characters and produces a result.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
/// <remarks>
/// <para>
/// This is the base interface for Farkle's parser API and encapsulates the lexical,
/// syntactical and semantic analysis stages. It supports high-performance parsing
/// of text from either a contiguous buffer or from streaming input.
/// </para>
/// <para>
/// Objects implementing <see cref="IParser{TChar, T}"/> must be stateless and thread-safe.
/// </para>
/// </remarks>
public interface IParser<TChar,T> : IServiceProvider
{
    /// <summary>
    /// Moves forward a parsing operation by parsing a block of characters.
    /// </summary>
    /// <param name="inputReader">Used to access the characters
    /// and the operation's <see cref="ParserState"/>.</param>
    /// <param name="completionState">Used to set that the operation
    /// has completed.</param>
    /// <remarks>
    /// <para>
    /// This method must be invoked after reading new characters from the input source.
    /// To determine how many characters in the buffer should be kept, compare the
    /// <see cref="ParserState.TotalCharactersRead"/> before and after running the parser.
    /// </para>
    /// <para>
    /// After a result has been set to <paramref name="completionState"/> the parsing operation
    /// has completed and the parser should not run again with the same parameters.
    /// </para>
    /// <para>
    /// For compatibility with Farkle's default streaming input parsing mechanism, running a parser
    /// whose <paramref name="inputReader"/>'s <see cref="ParserInputReader{TChar}.IsFinalBlock"/>
    /// property is set to <see langword="true"/> should set a result to <paramref name="completionState"/>.
    /// Custom parsers can support deviating from this behavior, but must do it in an opt-in fashion.
    /// </para>
    /// </remarks>
    void Run(ref ParserInputReader<TChar> inputReader, ref ParserCompletionState<T> completionState);
}
