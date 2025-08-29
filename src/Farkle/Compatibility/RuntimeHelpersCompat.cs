// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
global using RuntimeHelpersCompat = System.Runtime.CompilerServices.RuntimeHelpers;
#else
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Farkle.Compatibility;

[Embedded]
internal static class RuntimeHelpersCompat
{
    public static bool TryEnsureSufficientExecutionStack()
    {
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }
        catch (InsufficientExecutionStackException)
        {
            return false;
        }
        return true;
    }
}
#endif
