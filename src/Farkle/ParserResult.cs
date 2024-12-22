// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle;

/// <summary>
/// Represents the result of a parsing operation. It contains a
/// <typeparamref name="T"/> in case of success or an <see cref="object"/>
/// in case of failure.
/// </summary>
/// <typeparam name="T">The type of values held by successful parser
/// results.</typeparam>
public readonly struct ParserResult<T> : IFormattable
#if NET6_0_OR_GREATER
    , ISpanFormattable
#endif
{
    private readonly T _value;

    internal ParserResult(T value, object? error)
    {
        _value = value;
        Error = error;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Writes the <see cref="ParserResult{T}"/>'s success or error value to a span.
    /// </summary>
    /// <param name="destination">The span to write to.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="destination"/>.</param>
    /// <param name="format">Ignored.</param>
    /// <param name="provider">The <see cref="IFormatProvider"/> to use to format the string.</param>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        IsSuccess
            ? destination.TryWrite(provider, $"{Value}", out charsWritten)
            : destination.TryWrite(provider, $"{Error}", out charsWritten);
#endif

    private string ToString(IFormatProvider? formatProvider) =>
        IsSuccess
#if NET6_0_OR_GREATER
            ? string.Create(formatProvider, $"{Value}")
            : string.Create(formatProvider, $"{Error}");
#else
            ? ((FormattableString)$"{Value}").ToString(formatProvider)
            : ((FormattableString)$"{Error}").ToString(formatProvider);
#endif

    /// <summary>
    /// Converts the <see cref="ParserResult{T}"/>'s success or error value to a string.
    /// </summary>
    /// <param name="format">Ignored.</param>
    /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use to format the string.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ToString(formatProvider);

    /// <summary>
    /// Converts the <see cref="ParserResult{T}"/>'s success or error value to a string.
    /// </summary>
    public override string? ToString() => ToString(null, null);

    /// <summary>
    /// Whether the <see cref="ParserResult{T}"/> represents success.
    /// </summary>
    /// <seealso cref="Value"/>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Whether the <see cref="ParserResult{T}"/> represents failure.
    /// </summary>
    /// <seealso cref="Error"/>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsError => Error is not null;

    /// <summary>
    /// The <see cref="ParserResult{T}"/>'s success value.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsSuccess"/>
    /// is <see langword="false"/>.</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                ThrowHelpers.ThrowInvalidOperationException(Error.ToString());
            }
            return _value;
        }
    }

    /// <summary>
    /// The <see cref="ParserResult{T}"/>'s error value.
    /// </summary>
    /// <returns>
    /// An implementation-defined <see cref="object"/> that describes what went
    /// wrong with the parsing operation, or <see langword="null"/> if the parsing
    /// operation has succeeded.
    /// </returns>
    public object? Error { get; }
}

/// <summary>
/// Provides methods to create <see cref="ParserResult{T}"/> values.
/// </summary>
public static class ParserResult
{
    /// <summary>
    /// Creates a successful <see cref="ParserResult{T}"/>.
    /// </summary>
    /// <param name="value">The result's success value.</param>
    public static ParserResult<T> CreateSuccess<T>(T value) => new(value, null);

    /// <summary>
    /// Creates a failed <see cref="ParserResult{T}"/>.
    /// </summary>
    /// <param name="error">The result's error value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/>
    /// is <see langword="null"/>.</exception>
    public static ParserResult<T> CreateError<T>(object error)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(error);
        return new(default!, error);
    }
}
