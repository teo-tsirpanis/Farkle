// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Encapsulates the logic of converting sequences of characters into meaningful objects.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <typeparam name="T">The type of objects the parser produces in case of success.</typeparam>
/// <remarks>
/// <para>
/// This is the base interface for Farkle's parser API and encapsulates the lexical,
/// syntactical and semantic analysis stages. It supports high-performance parsing
/// of text from either a contiguous buffer or from streaming input.
/// </para>
/// <para>
/// <see cref="IParser{TChar, T}"/> is the lowest-level parser API of Farkle.
/// Higher-level APIs exist in the form of <see cref="ParserExtensions"/> and
/// <see cref="ParserStateContext{TChar, T}"/>.
/// </para>
/// <para>
/// Objects implementing <see cref="IParser{TChar, T}"/> must be stateless and thread-safe.
/// To provide additional functionality, <see cref="IParser{TChar, T}"/> inherits from the
/// <see cref="IServiceProvider"/> interface.
/// </para>
/// </remarks>
public interface IParser<TChar,T> : IServiceProvider
{
    /// <summary>
    /// Moves forward a parsing operation by parsing a block of characters.
    /// </summary>
    /// <param name="input">Used to access the characters
    /// and the operation's <see cref="ParserState"/>.</param>
    /// <param name="completionState">Used to set that the operation
    /// has completed.</param>
    /// <remarks>
    /// <para>
    /// This method must be invoked after reading new characters from the input source.
    /// To determine how many characters in the buffer should be kept, compare the
    /// <see cref="ParserState.TotalCharactersConsumed"/> before and after running the parser.
    /// </para>
    /// <para>
    /// After a result has been set to <paramref name="completionState"/> the parsing operation
    /// has completed and the parser should not run again with the same parameters.
    /// </para>
    /// <para>
    /// For compatibility with Farkle's higher-level parsing APIs, running a parser whose
    /// <paramref name="input"/>'s <see cref="ParserInputReader{TChar}.IsFinalBlock"/>
    /// property is set to <see langword="true"/> should set a result to <paramref name="completionState"/>.
    /// Custom parsers can support deviating from this behavior, but must do it in an opt-in fashion.
    /// </para>
    /// <para>
    /// While using this method to parse a single contiguous buffer is simple, directly using it
    /// to parse streaming input is non-trivial and requires manually managing the character buffer.
    /// Parsing streaming input is best done by using <see cref="ParserExtensions"/> or
    /// a <see cref="ParserStateContext{TChar, T}"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="ParserExtensions"/>
    /// <seealso cref="ParserStateContext"/>
    void Run(ref ParserInputReader<TChar> input, ref ParserCompletionState<T> completionState);
}
