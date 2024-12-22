// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic
{
    internal static class QueueCompat
    {
        public static bool TryDequeue<T>(this Queue<T> queue, [MaybeNullWhen(false)] out T result)
        {
            if (queue.Count == 0)
            {
                result = default;
                return false;
            }
            result = queue.Dequeue();
            return true;
        }
    }
}
#endif
