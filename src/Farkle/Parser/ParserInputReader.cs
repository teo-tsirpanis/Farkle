// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Farkle.Diagnostics;

namespace Farkle.Parser;

/// <summary>
/// Provides an interface for a parser to read characters and alter its <see cref="ParserState"/>.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <remarks>
/// This type is the replacement of the <c>Farkle.IO.CharStream</c> class of Farkle 6.
/// Contrary to that, this is a mutable <c>ref struct</c> that must be passed around by
/// reference and cannot be placed on the heap.
/// </remarks>
public ref struct ParserInputReader<TChar>
{
    private readonly ref ParserState _state;

    /// <summary>
    /// The parser's state.
    /// </summary>
    public readonly ref ParserState State => ref _state;

    /// <summary>
    /// The remaining available characters.
    /// </summary>
    public ReadOnlySpan<TChar> RemainingCharacters { get; private set; }

    /// <summary>
    /// Whether there will be no other characters available after
    /// <see cref="RemainingCharacters"/>.
    /// </summary>
    public bool IsFinalBlock { get; }

    /// <summary>
    /// Whether input has ended. This happens when <see cref="RemainingCharacters"/>
    /// is empty and <see cref="IsFinalBlock"/> is <see langword="true"/>.
    /// </summary>
    public readonly bool IsEndOfInput => RemainingCharacters.IsEmpty && IsFinalBlock;

    /// <summary>
    /// Creates a <see cref="ParserInputReader{TChar}"/>.
    /// </summary>
    /// <param name="state">A reference to the reader's <see cref="ParserState"/>.</param>
    /// <param name="characters">The value that will be assigned to <see cref="RemainingCharacters"/>.</param>
    /// <param name="isFinal">The value that will be assigned to <see cref="IsFinalBlock"/>.</param>
    public ParserInputReader(ref ParserState state, ReadOnlySpan<TChar> characters, bool isFinal = true)
    {
        _state = ref state;
        RemainingCharacters = characters;
        IsFinalBlock = isFinal;
    }

    /// <summary>
    /// Consumes the first characters of <see cref="RemainingCharacters"/>
    /// and makes them unavailable for future reads.
    /// </summary>
    /// <param name="count">The number of characters to consume.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is
    /// negative or greater than the length of <see cref="RemainingCharacters"/>.</exception>
    /// <remarks>
    /// This method updates <see cref="RemainingCharacters"/> and the
    /// <see cref="ParserState.CurrentPosition"/> and <see cref="ParserState.TotalCharactersConsumed"/>
    /// properties of <see cref="State"/>.
    /// </remarks>
    /// <seealso cref="ParserState.GetPositionAfter"/>
    public void Consume(int count)
    {
        if ((uint)count > (uint)RemainingCharacters.Length)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(count));
        }
        State.Consume(RemainingCharacters[..count]);
        RemainingCharacters = RemainingCharacters[count..];
        if (RemainingCharacters.IsEmpty && IsFinalBlock)
        {
            State.CompleteInput();
        }
    }

    /// <summary>
    /// Throws a <see cref="ParserApplicationException"/> indicating an error at
    /// the specified offset in the remaining characters.
    /// </summary>
    /// <param name="offset">The number of characters after
    /// <see cref="ParserState.CurrentPosition"/> at which to throw the exception.</param>
    /// <param name="message">The object to use as the exception's message.</param>
    [DoesNotReturn]
    public readonly void FailAtOffset(int offset, object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        TextPosition position = State.GetPositionAfter(RemainingCharacters[..offset]);
        throw new ParserApplicationException(new ParserDiagnostic(position, message), autoSetPosition: false);
    }
}
