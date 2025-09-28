// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using System.Runtime.CompilerServices;

namespace Farkle;

internal static partial class Compatibility
{
    extension(RuntimeHelpers)
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
}
#endif
