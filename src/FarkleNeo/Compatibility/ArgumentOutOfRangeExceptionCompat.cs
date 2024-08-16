// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NET8_0_OR_GREATER
global using ArgumentOutOfRangeExceptionCompat = System.ArgumentOutOfRangeException;
#else
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Compatibility;

internal static class ArgumentOutOfRangeExceptionCompat
{
    [DoesNotReturn, StackTraceHidden]
    private static void ThrowNegative<T>(T value, string? paramName) =>
        throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");

    [StackTraceHidden]
    public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            ThrowNegative(value, paramName);
    }
}
#endif
