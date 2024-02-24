// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic
{
    internal static class StackCompat
    {
        public static bool TryPop<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T result)
        {
            if (stack.Count == 0)
            {
                result = default;
                return false;
            }
            result = stack.Pop();
            return true;
        }
    }
}
#endif
