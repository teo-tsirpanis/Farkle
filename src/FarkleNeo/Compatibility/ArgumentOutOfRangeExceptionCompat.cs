// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

// TODO: Redirect to ArgumentOutOfRangeException once we target .NET 8.
internal static class ArgumentOutOfRangeExceptionCompat
{
    [DoesNotReturn, StackTraceHidden]
    private static void ThrowNegative<T>(T value, string? paramName) =>
        throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");

    public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            ThrowNegative(value, paramName);
    }
}
