// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle;

internal static class ThrowHelpers
{
    [DoesNotReturn, StackTraceHidden]
    public static void ThrowInvalidOperationException(string? message = null) =>
        throw new InvalidOperationException(message);

    [DoesNotReturn, StackTraceHidden]
    public static void ThrowNotSupportedException(string? message = null) =>
        throw new NotSupportedException(message);
}
