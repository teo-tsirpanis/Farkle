// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if (NETCOREAPP && !NET7_0_OR_GREATER) || NETSTANDARD2_1_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Farkle.Parser;

/// <summary>
/// Provides an interface for a parser to read characters.
/// </summary>
/// <typeparam name="TChar">The type of characters.</typeparam>
/// <remarks>
/// This type is the replacement of the <code>Farkle.IO.CharStream</code> code of Farkle 6.
/// Contrary to that, this is a mutable value type that must be passed around by reference.
/// </remarks>
public ref struct ParserInputReader<TChar>
{
#if NET7_0_OR_GREATER
    private readonly ref ParserState _state;
#elif NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    private readonly Span<ParserState> _state;
#else
    private readonly IParserStateBox _stateBox;
#endif

    /// <summary>
    /// The parser's state.
    /// </summary>
    public readonly ref ParserState State =>
#if NET7_0_OR_GREATER
        ref _state;
#elif NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        ref MemoryMarshal.GetReference(_state);
#else
        ref _stateBox.State;
#endif

    /// <summary>
    /// The remaining available characters.
    /// </summary>
    public ReadOnlySpan<TChar> RemainingCharacters { get; private set; }

    /// <summary>
    /// Whether there will be no other characters available after
    /// <see cref="RemainingCharacters"/>.
    /// </summary>
    public bool IsFinalBlock { get; }

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    /// <summary>
    /// Creates a <see cref="ParserInputReader{TChar}"/>.
    /// </summary>
    /// <param name="state">A reference to the reader's <see cref="ParserState"/>.</param>
    /// <param name="characters">The value that will be assigned to <see cref="RemainingCharacters"/>.</param>
    /// <param name="isFinal">The value that will be assigned to <see cref="IsFinalBlock"/>.</param>
#if NET7_0_OR_GREATER
    public
#else
    // On frameworks earlier than .NET 7 we cannot be sure that the ref
    // safety rules are enforced and therefore cannot make it public.
    internal
#endif
        ParserInputReader(ref ParserState state, ReadOnlySpan<TChar> characters, bool isFinal = true)
    {
#if NET7_0_OR_GREATER
        _state = ref state;
#else
        _state = MemoryMarshal.CreateSpan(ref state, 1);
#endif
        RemainingCharacters = characters;
        IsFinalBlock = isFinal;
    }
#endif

    /// <summary>
    /// Creates a <see cref="ParserInputReader{TChar}"/>.
    /// </summary>
    /// <param name="stateBox">An <see cref="IParserStateBox"/> containing a reference to the
    /// reader's <see cref="ParserState"/>.</param>
    /// <param name="characters">The value that will be assigned to <see cref="RemainingCharacters"/>.</param>
    /// <param name="isFinal">The value that will be assigned to <see cref="IsFinalBlock"/>.</param>
    /// <remarks>
    /// Callers should not assume that the <paramref name="stateBox"/> instance will be
    /// kept alive by the garbage collector on all frameworks.
    /// </remarks>
    public ParserInputReader(IParserStateBox stateBox, ReadOnlySpan<TChar> characters, bool isFinal = true)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(stateBox);
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        this = new(ref stateBox.State, characters, isFinal);
#else
        _stateBox = stateBox;
        RemainingCharacters = characters;
        IsFinalBlock = isFinal;
#endif
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
    }
}
