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
public readonly struct ParserResult<T>
{
    private readonly T _value;

    internal ParserResult(T value, object? error)
    {
        _value = value;
        Error = error;
    }

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
