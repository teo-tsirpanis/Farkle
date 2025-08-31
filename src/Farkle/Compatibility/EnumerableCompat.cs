// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET7_0_OR_GREATER
using Microsoft.CodeAnalysis;

namespace System.Linq
{
    [Embedded]
    internal static class EnumerableCompat
    {
        public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> enumerable) => enumerable.OrderBy(x => x);
    }
}
#endif
