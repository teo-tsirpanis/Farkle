// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Farkle.Diagnostics;

namespace Farkle.Parser;

/// <summary>
/// Provides extension methods on <see cref="ParserInputReader{TChar}"/>.
/// </summary>
public static class ParserInputReaderExtensions
{
    /// <summary>
    /// Throws a <see cref="ParserApplicationException"/> at the specified offset
    /// in the remaining characters of a <see cref="ParserInputReader{TChar}"/>.
    /// </summary>
    /// <typeparam name="TChar">The type of characters that are parsed.</typeparam>
    /// <param name="reader">The parser input reader.</param>
    /// <param name="offset">The number of characters after <paramref name="reader"/>'s
    /// <see cref="ParserState.CurrentPosition"/> at which to throw the exception.</param>
    /// <param name="message">The object to use as the exception's message.</param>
    [DoesNotReturn]
    public static void FailAtOffset<TChar>(this in ParserInputReader<TChar> reader, int offset, object message)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(message);
        TextPosition position = reader.State.GetPositionAfter(reader.RemainingCharacters[..offset]);
        throw new ParserApplicationException(new ParserDiagnostic(position, message), autoSetPosition: false);
    }
}
