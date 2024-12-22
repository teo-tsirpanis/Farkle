// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NET6_0_OR_GREATER
global using ArgumentNullExceptionCompat = System.ArgumentNullException;
#else
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Compatibility;

internal static class ArgumentNullExceptionCompat
{
    [StackTraceHidden]
    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            ThrowHelpers.ThrowArgumentNullException(paramName);
    }
}
#endif
