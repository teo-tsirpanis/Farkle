// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Tracks the current position of text being parsed.
/// </summary>
/// <remarks>
/// This class has to be stateful to correctly handle CR and CRLF line endings.
/// Both CR and LF move the <see cref="Position"/> to the next line, but an LF after
/// a CR does not. Therefore besides the position we also store whether the last
/// character we saw is a CR, because the CR and the LF might be submitted separately.
/// </remarks>
internal struct PositionTracker
{
    private bool _lastSeenCr;

    /// <summary>
    /// The current position.
    /// </summary>
    public TextPosition Position { get; private set; }

    /// <summary>
    /// Gets the position of the last of the given characters,
    /// assuming they start at <see cref="Position"/>.
    /// </summary>
    /// <typeparam name="T">The type of characters.</typeparam>
    /// <param name="span">A <see cref="ReadOnlySpan{T}"/> of characters.</param>
    /// <remarks>
    /// <para>
    /// The position's column number is incremented for each character.
    /// If <typeparamref name="T"/> is <see cref="char"/> or <see cref="byte"/>, the
    /// line number is incremented and the column number is reset for each CR, LF or
    /// CRLF sequence.
    /// </para>
    /// <para>
    /// CR characters at the end of <paramref name="span"/> do not change the line number.
    /// </para>
    /// </remarks>
    /// <seealso cref="Advance{T}(ReadOnlySpan{T})"/>
    public readonly TextPosition GetPositionAfter<T>(ReadOnlySpan<T> span)
    {
        TextPosition pos = Position;
        if (!span.IsEmpty)
        {
            // If the last character we saw was a CR, and the current character is not an LF,
            // move to the next line, which we did not do last time.
            if (_lastSeenCr
                && ((typeof(T) == typeof(char) && (char)(object)span[0]! != '\n')
                || (typeof(T) == typeof(byte) && (byte)(object)span[0]! != (byte)'\n')))
            {
                pos = pos.NextLine();
            }
            pos = pos.Advance(span);
        }
        return pos;
    }

    /// <summary>
    /// Advances <see cref="Position"/> to the position of the last of the given characters.
    /// </summary>
    /// <typeparam name="T">The type of characters.</typeparam>
    /// <param name="span">A <see cref="ReadOnlySpan{T}"/> of characters.</param>
    /// <remarks>
    /// This method remembers whether the last character in <paramref name="span"/> is a CR.
    /// </remarks>
    /// <seealso cref="GetPositionAfter"/>
    public void Advance<T>(ReadOnlySpan<T> span)
    {
        if (!span.IsEmpty)
        {
            Position = GetPositionAfter(span);
            _lastSeenCr = (typeof(T) == typeof(char) && (char)(object)span[^1]! == '\r')
                || (typeof(T) == typeof(byte) && (byte)(object)span[^1]! == (byte)'\r');
        }
    }

    /// <summary>
    /// Marks the end of input.
    /// </summary>
    /// <remarks>
    /// The line number of <see cref="Position"/> is incremented if the last
    /// character submitted in <see cref="Advance{T}(ReadOnlySpan{T})"/> is a CR.
    /// </remarks>
    public void CompleteInput()
    {
        // If the last character we saw was a CR, we will not see anything after that,
        // so move to the next line and reset _lastSeenCr.
        if (_lastSeenCr)
        {
            _lastSeenCr = false;
            Position = Position.NextLine();
        }
    }
}
