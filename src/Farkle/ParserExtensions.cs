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
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        ParserState state = new();
        ParserInputReader<TChar> inputReader = new(ref state, s, true);
#else
        ParserStateBox stateBox = new();
        ParserInputReader<TChar> inputReader = new(stateBox, s, true);
#endif
        ParserCompletionState<T> completionState = new();
        parser.Run(ref inputReader, ref completionState);
        return completionState.Result;
    }

    private static ParserResult<T> RunContext<T>(ParserStateContext<char, T> context, TextReader reader, bool keepReaderOpen)
    {
        try
        {
            while (!context.IsCompleted)
            {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                int read = reader.Read(context.GetSpan());
#else
                ArraySegment<char> segment = context.GetArraySegment();
                int read = reader.Read(segment.Array, segment.Offset, segment.Count);
#endif
                if (read == 0)
                {
                    context.CompleteInput();
                    break;
                }
                context.Advance(read);
            }
            return context.Result;
        }
        finally
        {
            if (!keepReaderOpen)
            {
                reader.Dispose();
            }
        }
    }

    private static async ValueTask<ParserResult<T>> RunContextAsync<T>(ParserStateContext<char, T> context, TextReader reader, bool keepReaderOpen, CancellationToken cancellationToken)
    {
        try
        {
            while (!context.IsCompleted)
            {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                int read = await reader.ReadAsync(context.GetMemory(), cancellationToken);
#else
                ArraySegment<char> segment = context.GetArraySegment();
                int read = await (cancellationToken.IsCancellationRequested ?
                    Task.FromCanceled<int>(cancellationToken) :
                    reader.ReadAsync(segment.Array, segment.Offset, segment.Count));
#endif
                if (read == 0)
                {
                    context.CompleteInput();
                    break;
                }
                context.Advance(read);
            }
            return context.Result;
        }
        finally
        {
            if (!keepReaderOpen)
            {
                reader.Dispose();
            }
        }
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
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
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
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
        ArgumentNullExceptionCompat.ThrowIfNull(s);
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
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
        ArgumentNullExceptionCompat.ThrowIfNull(reader);

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        return RunContext(context, reader, keepReaderOpen: true);
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
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
        ArgumentNullExceptionCompat.ThrowIfNull(path);

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        context.State.InputName = path;
        // We don't need buffering in the FileStream; both the context and the StreamReader have.
        TextReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1));
        return RunContext(context, reader, keepReaderOpen: false);
    }

    /// <summary>
    /// Asynchronously parses a stream of characters read from a <see cref="TextReader"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="reader">The <see cref="TextReader"/> to read the characters from.</param>
    /// <param name="cancellationToken">Used to cancel the parsing operation. Optional.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that will return a <see cref="ParserResult{T}"/>
    /// containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="reader"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="reader"/> will be read from until it ends or the parsing operation fails.
    /// <paramref name="reader"/> will not be automatically disposed.
    /// </para>
    /// <para>
    /// On frameworks not compatible with .NET Standard 2.1, cancelling the operation will not
    /// have effect until the next time the characters are read. This is due to
    /// <see cref="TextReader"/> not supporting passing a <see cref="CancellationToken"/> to its
    /// <c>ReadAsync</c> method.
    /// </para>
    /// </remarks>
    public static ValueTask<ParserResult<T>> ParseAsync<T>(this IParser<char, T> parser, TextReader reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
        ArgumentNullExceptionCompat.ThrowIfNull(reader);

        if (cancellationToken.IsCancellationRequested)
        {
            // ValueTask.FromCanceled is not available in all frameworks.
            // We use Task.FromCanceled and wrap it in a ValueTask, which
            // is equivalent.
            return new(Task.FromCanceled<ParserResult<T>>(cancellationToken));
        }

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        return RunContextAsync(context, reader, keepReaderOpen: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously parses the content of a file. This method wraps <see cref="ParseAsync"/>.
    /// </summary>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="path">The path to the file to parse.</param>
    /// <param name="cancellationToken">Used to cancel the parsing operation. Optional.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that will return a <see cref="ParserResult{T}"/>
    /// containing the result of the parsing operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> or <paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The file will be read from until it ends or until the parsing operation fails.
    /// </para>
    /// <para>
    /// On frameworks not compatible with .NET Standard 2.1, cancelling the operation will not
    /// have effect until the next time the characters are read. This is due to
    /// <see cref="TextReader"/> not supporting passing a <see cref="CancellationToken"/> to its
    /// <c>ReadAsync</c> method.
    /// </para>
    /// </remarks>
    public static ValueTask<ParserResult<T>> ParseFileAsync<T>(this IParser<char, T> parser, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(parser);
        ArgumentNullExceptionCompat.ThrowIfNull(path);

        if (cancellationToken.IsCancellationRequested)
        {
            return new(Task.FromCanceled<ParserResult<T>>(cancellationToken));
        }

        ParserStateContext<char, T> context = ParserStateContext.Create(parser);
        context.State.InputName = path;
        // We don't need buffering in the FileStream; both the context and the StreamReader have.
        TextReader reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, true));
        return RunContextAsync(context, reader, keepReaderOpen: false, cancellationToken: cancellationToken);
    }
}
