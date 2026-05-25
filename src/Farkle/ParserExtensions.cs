// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser;

namespace Farkle;

/// <summary>
/// Provides extension methods on <see cref="IParser{TChar, T}"/>
/// to easily parse text from various sources.
/// </summary>
/// <remarks>
/// This is the highest-level parser API of Farkle. It is recommended
/// for most use cases.
/// </remarks>
public static class ParserExtensions
{
    private static ParserResult<T> ParseCore<TChar, T>(this IParser<TChar, T> parser, ReadOnlySpan<TChar> s)
    {
        ParserState state = new();
        ParserInputReader<TChar> inputReader = new(ref state, s, true);
        ParserCompletionState<T> completionState = new();
        parser.Run(ref inputReader, ref completionState);
        return completionState.Result;
    }

    private static ParserResult<T> RunContext<T>(ParserStateContext<char, T> context, TextReader reader)
    {
        while (!context.IsCompleted)
        {
            int read = reader.Read(context.GetSpan());
            if (read == 0)
            {
                context.CompleteInput();
                break;
            }
            context.Advance(read);
        }
        return context.Result;
    }

    private static async Task<ParserResult<T>> RunContextAsync<T>(ParserStateContext<char, T> context, TextReader reader, CancellationToken cancellationToken)
    {
        while (!context.IsCompleted)
        {
            int read = await reader.ReadAsync(context.GetMemory(), cancellationToken);
            if (read == 0)
            {
                context.CompleteInput();
                break;
            }
            context.Advance(read);
        }
        return context.Result;
    }

    /// <summary>
    /// Parses a <see cref="ReadOnlySpan{TChar}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters.</typeparam>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="s">The span to parse.</param>
    /// <returns>A <see cref="ParserResult{T}"/> containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
    public static ParserResult<T> Parse<TChar, T>(this IParser<TChar, T> parser, ReadOnlySpan<TChar> s)
    {
        ArgumentNullException.ThrowIfNull(parser);
        return parser.ParseCore(s);
    }

    /// <summary>
    /// Parses a <see cref="string"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="s">The string to parse.</param>
    /// <returns>A <see cref="ParserResult{T}"/> containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="s"/> is
    /// <see langword="null"/>.</exception>
    public static ParserResult<T> Parse<T>(this IParser<char, T> parser, string s)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(s);
        return parser.ParseCore(s.AsSpan());
    }

    /// <summary>
    /// Parses a stream of characters read from a <see cref="TextReader"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="reader">The <see cref="TextReader"/> to read the characters from.</param>
    /// <returns>A <see cref="ParserResult{T}"/> containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="reader"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// <paramref name="reader"/> will be read from until it ends or the parsing operation fails.
    /// <paramref name="reader"/> will not be automatically disposed.
    /// </remarks>
    public static ParserResult<T> Parse<T>(this IParser<char, T> parser, TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(reader);

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        return RunContext(context, reader);
    }

    /// <summary>
    /// Parses the content of a file. This method wraps <see cref="Parse{T}(IParser{char, T}, TextReader)"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="path">The path to the file to parse.</param>
    /// <returns>A <see cref="ParserResult{T}"/> containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// The file will be read from until it ends or until the parsing operation fails.
    /// </remarks>
    public static ParserResult<T> ParseFile<T>(this IParser<char, T> parser, string path)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        context.State.InputName = path;
        // We don't need buffering in the FileStream; both the context and the StreamReader have.
        using TextReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1));
        return RunContext(context, reader);
    }

    /// <summary>
    /// Asynchronously parses a stream of characters read from a <see cref="TextReader"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="reader">The <see cref="TextReader"/> to read the characters from.</param>
    /// <param name="cancellationToken">Used to cancel the parsing operation. Optional.</param>
    /// <returns>A <see cref="Task{TResult}"/> that will return a <see cref="ParserResult{T}"/>
    /// containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="reader"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// <paramref name="reader"/> will be read from until it ends or the parsing operation fails.
    /// <paramref name="reader"/> will not be automatically disposed.
    /// </remarks>
    public static async Task<ParserResult<T>> ParseAsync<T>(this IParser<char, T> parser, TextReader reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(reader);

        cancellationToken.ThrowIfCancellationRequested();

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        return await RunContextAsync(context, reader, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously parses the content of a file. This method wraps <see cref="ParseAsync"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="path">The path to the file to parse.</param>
    /// <param name="cancellationToken">Used to cancel the parsing operation. Optional.</param>
    /// <returns>A <see cref="Task{TResult}"/> that will return a <see cref="ParserResult{T}"/>
    /// containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// The file will be read from until it ends or until the parsing operation fails.
    /// </remarks>
    public static async Task<ParserResult<T>> ParseFileAsync<T>(this IParser<char, T> parser, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);

        cancellationToken.ThrowIfCancellationRequested();

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        context.State.InputName = path;
        // We don't need buffering in the FileStream; both the context and the StreamReader have.
        using TextReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, true));
        return await RunContextAsync(context, reader, cancellationToken: cancellationToken);
    }
}
