// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET8_0_OR_GREATER
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle;

internal static partial class Compatibility
{
    extension(ArgumentOutOfRangeException)
    {
        [StackTraceHidden]
        public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < 0)
                ThrowNegative(value, paramName);

            [DoesNotReturn, StackTraceHidden]
            static void ThrowNegative<T>(T value, string? paramName) =>
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
        }
    }
}
#endif
