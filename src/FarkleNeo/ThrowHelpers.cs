// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle;

internal static class ThrowHelpers
{
    [DoesNotReturn, StackTraceHidden]
    public static void ThrowArgumentException(string? parameterName, string? message = null, Exception? innerException = null)
        => throw new ArgumentException(parameterName, message, innerException);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowArgumentOutOfRangeException(string? parameterName, string? message = null)
        => throw new ArgumentOutOfRangeException(parameterName, message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowEndOfStreamException(string? message = null) =>
        throw new EndOfStreamException(message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowInvalidDataException(string? message = null, Exception? innerException = null) =>
        throw new InvalidDataException(message, innerException);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowKeyNotFoundException(string? message = null) =>
        throw new KeyNotFoundException(message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowInvalidOperationException(string? message = null) =>
        throw new InvalidOperationException(message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowNotSupportedException(string? message = null) =>
        throw new NotSupportedException(message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowOutOfMemoryException(string? message = null) =>
        throw new OutOfMemoryException(message);
}
