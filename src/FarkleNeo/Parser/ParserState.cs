// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Farkle.Parser;

/// <summary>
/// Contains all state of a parsing operation.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ParserState"/> contains things such as the parser's current
/// position, and a key-value store of objects that can be used by the parser
/// and its extensions to store arbitrary state (such as symbol tables for a
/// programming language).
/// </para>
/// <para>
/// This is a mutable value type that must be passed around by reference.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public struct ParserState
{
    private PositionTracker _positionTracker;
    private Dictionary<object, object> _stateDictionary;

    /// <summary>
    /// The position of the last character the parser has consumed.
    /// </summary>
    /// <remarks>
    /// In transformers, this is the position of the <em>first</em>
    /// character of the token, analogous to the
    /// <code>ITransformerContext.TokenStartPosition</code> property of Farkle 6.
    /// </remarks>
    /// <seealso cref="ParserInputReader{TChar}.Consume"/>
    public readonly TextPosition CurrentPosition => _positionTracker.Position;

    /// <summary>
    /// The number of characters the parser has consumed.
    /// </summary>
    /// <seealso cref="ParserInputReader{TChar}.Consume"/>
    public long TotalCharactersConsumed { get; private set; }

    /// <summary>
    /// An implementation-specific object that can hold additional state without
    /// the allocation overhead of <see cref="SetValue"/>.
    /// </summary>
    /// <remarks>
    /// The object held by this property is intended to be used by the main parser code
    /// and not user extensions to parser.
    /// </remarks>
    public object? Context { get; init; }

    /// <summary>
    /// A user-provided identifier for the input being parsed.
    /// </summary>
    /// <remarks>
    /// When parsing a file this could be the file's path.
    /// </remarks>
    public string? InputName { get; set; }

    internal void Consume<T>(ReadOnlySpan<T> characters)
    {
        _positionTracker.Advance(characters);
        TotalCharactersConsumed += characters.Length;
    }

    /// <summary>
    /// Gets the position of the last of the given characters,
    /// assuming they start at <see cref="CurrentPosition"/>.
    /// </summary>
    /// <typeparam name="T">The type of characters.</typeparam>
    /// <param name="characters">A <see cref="ReadOnlySpan{T}"/> of characters.</param>
    /// <remarks>
    /// <para>
    /// The position's column number is incremented for each character.
    /// If <typeparamref name="T"/> is <see cref="char"/> or <see cref="byte"/>, the
    /// line number is incremented and the column number is reset for each CR, LF or
    /// CRLF sequence.
    /// </para>
    /// <para>
    /// CR characters at the end of <paramref name="characters"/> do not change the line number.
    /// </para>
    /// </remarks>
    public readonly TextPosition GetPositionAfter<T>(ReadOnlySpan<T> characters) =>
        _positionTracker.GetPositionAfter(characters);

    /// <summary>
    /// Sets a value in the <see cref="ParserState"/>'s key-value dictionary.
    /// </summary>
    /// <param name="key">The key of the value to set.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or
    /// <paramref name="value"/> is <see langword="null"/>.</exception>
    public void SetValue(object key, object value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(key);
        ArgumentNullExceptionCompat.ThrowIfNull(value);
        (_stateDictionary ??= new())[key] = value;
    }

    /// <summary>
    /// Returns the value associated with the given key in the <see cref="ParserState"/>'s
    /// key-value dictionary, if it exists.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">Will be assigned the value corresponding to
    /// <paramref name="key"/>, or <see langword="null"/> if such value
    /// does not exist.</param>
    /// <returns>Whether the key exists in the dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public readonly bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(key);
        value = null;
        return _stateDictionary?.TryGetValue(key, out value) ?? false;
    }

    /// <summary>
    /// Removes the value associated with the given key from the <see cref="ParserState"/>'s
    /// key-value dictionary.
    /// </summary>
    /// <param name="key">The key of a value to remove.</param>
    /// <returns>Whether the key had existed in the dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool RemoveValue(object key)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(key);
        return _stateDictionary?.Remove(key) ?? false;
    }
}
