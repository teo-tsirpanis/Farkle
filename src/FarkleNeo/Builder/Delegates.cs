// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Parser;

namespace Farkle.Builder;

/// <summary>
/// Represents a semantic action that gets executed when a terminal or equivalent
/// is encountered in input and produces a value.
/// </summary>
/// <typeparam name="TChar">The type of characters of the input text.</typeparam>
/// <typeparam name="T">The type of the produced value.</typeparam>
/// <param name="state">A reference to the parser's state.
/// This is a "controlled-mutability" reference. It can be modified
/// through its members, but cannot be reassigned to a different
/// <see cref="ParserState"/>.</param>
/// <param name="input">The characters of the input that matched the terminal.</param>
public delegate T Transformer<TChar, out T>(ref ParserState state, ReadOnlySpan<TChar> input);

internal delegate T Fuser<out T>(ref ParserState state, Span<object?> input);

internal static class Transformer
{
    public static Transformer<TChar, T?> GetIdentity<TChar, T>() where T : class? =>
        (ref ParserState state, ReadOnlySpan<TChar> input) => default;

    public static Transformer<TChar, object?> Box<TChar, T>(Transformer<TChar, T> transformer)
    {
        // Passing null in transformer will not be immediately noticed.
        // Transformer should be checked for null by the calling public API.
        Debug.Assert(transformer is not null);
        // Take advantage of covariance and avoid an extra level of indirection if
        // T is a reference type.
        if (transformer is Transformer<TChar, object?> alreadyCasted)
            return alreadyCasted;
        return (ref ParserState state, ReadOnlySpan<TChar> input) => transformer(ref state, input)!;
    }
}
