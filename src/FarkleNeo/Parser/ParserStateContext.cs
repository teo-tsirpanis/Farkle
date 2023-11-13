// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace Farkle.Parser;

/// <summary>
/// Manages the lifetime of a parsing operation on streaming input.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <remarks>
/// <para>
/// Parser state contexts are the intermediate-level Parser API of Farkle, sitting on
/// top of <see cref="IParser{TChar, T}"/>. They contain the logic to parse text, to
/// keep the parsing operation's <see cref="ParserState"/>, to manage the character
/// buffer, and bring them all together.
/// </para>
/// <para>
/// Parser state contexts implement the <see cref="IBufferWriter{TChar}"/> interface
/// to allow writing characters in an efficient fashion. The parsing logic is automatically
/// invoked when new characters are submitted through the <see cref="Advance"/> method,
/// or when input ends with the <see cref="CompleteInput"/> method.
/// </para>
/// <para>
/// This class cannot be inherited by user code.
/// </para>
/// </remarks>
/// <seealso cref="ParserStateContext{TChar, T}"/>
/// <seealso cref="ParserStateContext.Create"/>
public abstract class ParserStateContext<TChar> : IParserStateBox, IBufferWriter<TChar>
{
    private ParserState _state;
    private CharacterBufferManager<TChar> _bufferManager;

    private protected ParserStateContext(ParserStateContextOptions? options = null)
    {
        _state = new() { Context = this };
        _bufferManager = new(options?.InitialBufferSize ?? ParserStateContextOptions.DefaultInitialBufferSize);
    }

    private protected ParserInputReader<TChar> GetInputReader() =>
        new(this, _bufferManager.UsedCharacters, _bufferManager.IsInputCompleted);

    private protected void UpdateBufferState(long totalCharactersConsumed, bool isCompleted) =>
        _bufferManager.UpdateStateFromParser(totalCharactersConsumed, isCompleted);

    private protected abstract void Run();

    /// <summary>
    /// The parsing operation's state.
    /// </summary>
    /// <remarks>
    /// The <see cref="ParserState.Context"/> property will be equal to
    /// <see langword="this"/> object.
    /// </remarks>
    public ref ParserState State => ref _state;

    /// <summary>
    /// Whether the parsing operation has completed.
    /// </summary>
    public abstract bool IsCompleted { get; }

    /// <summary>
    /// Resets the <see cref="ParserStateContext{TChar}"/>, allowing it to
    /// be reused for another parsing operation.
    /// </summary>
    /// <remarks>
    /// This method cannot be overridden by user code. To provide custom resetting
    /// logic override <see cref="ParserStateContext{TChar, T}.OnReset"/> instead.
    /// </remarks>
    public virtual void Reset()
    {
        _bufferManager.Reset();
        _state = new() { Context = this };
    }

    /// <inheritdoc/>
    public Memory<TChar> GetMemory(int sizeHint = 0) => _bufferManager.GetMemory(sizeHint);

    /// <inheritdoc/>
    public Span<TChar> GetSpan(int sizeHint = 0) => _bufferManager.GetSpan(sizeHint);

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
    internal ArraySegment<TChar> GetArraySegment(int sizeHint = 0) => _bufferManager.GetArraySegment(sizeHint);
#endif

    /// <inheritdoc/>
    public void Advance(int count)
    {
        _bufferManager.Advance(count);
        Run();
    }

    /// <summary>
    /// Signals to the <see cref="ParserStateContext{TChar}"/> that no more input
    /// </summary>
    public void CompleteInput()
    {
        _bufferManager.CompleteInput();
        Run();
    }
}

/// <summary>
/// Extends <see cref="ParserStateContext{TChar}"/> with the ability to return a result
/// and some user-facing extensibility points.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
/// <remarks>
/// Unlike <see cref="ParserStateContext{TChar}"/> this class can be inherited but user code
/// rarely needs to do it. To create a <see cref="ParserStateContext{TChar, T}"/> use the
/// <see cref="ParserStateContext.Create"/> method.
/// </remarks>
public abstract class ParserStateContext<TChar, T> : ParserStateContext<TChar>
{
    private ParserCompletionState<T> _completionState;

    /// <summary>
    /// Creates a <see cref="ParserStateContext{TChar, T}"/>.
    /// </summary>
    /// <param name="options">Options to configure the context. Optional.</param>
    protected ParserStateContext(ParserStateContextOptions? options = null) : base(options) { }

    /// <inheritdoc/>
    public sealed override bool IsCompleted => _completionState.IsCompleted;

    /// <summary>
    /// The result of the parsing operation.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsCompleted"/>
    /// is <see langword="false"/>.</exception>
    /// <seealso cref="IsCompleted"/>
    public ParserResult<T> Result => _completionState.Result;

    /// <inheritdoc/>
    public sealed override void Reset()
    {
        base.Reset();
        _completionState = default;
        OnReset();
    }

    private protected sealed override void Run()
    {
        try
        {
            ParserInputReader<TChar> inputReader = GetInputReader();
            Run(ref inputReader, ref _completionState);
        }
        finally
        {
            UpdateBufferState(State.TotalCharactersConsumed, _completionState.IsCompleted);
        }
    }

    /// <summary>
    /// Provides an extensibility point for the <see cref="ParserStateContext{TChar, T}"/>'s
    /// resetting logic. This method gets called by <see cref="Reset"/>.
    /// </summary>
    protected virtual void OnReset() { }

    /// <summary>
    /// Invokes the parsing logic of the <see cref="ParserStateContext{TChar, T}"/>.
    /// </summary>
    /// <param name="input">Used to access the characters
    /// and the operation's <see cref="ParserState"/>.</param>
    /// <param name="completionState">Used to set that the operation
    /// has completed.</param>
    /// <seealso cref="IParser{TChar, T}.Run"/>
    protected abstract void Run(ref ParserInputReader<TChar> input, ref ParserCompletionState<T> completionState);
}

/// <summary>
/// Provides methods to create <see cref="ParserStateContext{TChar, T}"/> objects.
/// </summary>
public static class ParserStateContext
{
    private sealed class DefaultContext<TChar, T>(IParser<TChar, T> parser, ParserStateContextOptions? options) : ParserStateContext<TChar, T>(options)
    {
        protected override void Run(ref ParserInputReader<TChar> input, ref ParserCompletionState<T> completionState) =>
            parser.Run(ref input, ref completionState);
    }

    /// <summary>
    /// Creates a <see cref="ParserStateContext{TChar, T}"/> from an <see cref="IParser{TChar, T}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters that are parsed. Usually it is
    /// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
    /// parsers).</typeparam>
    /// <typeparam name="T">The type of result the parser produces in case of success.</typeparam>
    /// <param name="parser">The <see cref="IParser{TChar, T}"/> to use.</param>
    /// <param name="options">Options to configure the context. Optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
    public static ParserStateContext<TChar, T> Create<TChar, T>(IParser<TChar, T> parser, ParserStateContextOptions? options = null)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(parser);

        if (parser.GetService(typeof(IParserStateContextFactory<TChar, T>)) is IParserStateContextFactory<TChar, T> factory)
        {
            return factory.CreateContext(options);
        }

        return new DefaultContext<TChar, T>(parser, options);
    }
}
