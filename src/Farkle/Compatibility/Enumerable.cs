// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET7_0_OR_GREATER
namespace Farkle;

internal static partial class Compatibility
{
    extension<T>(IEnumerable<T> enumerable)
    {
        public IOrderedEnumerable<T> Order() => enumerable.OrderBy(x => x);
    }
}
#endif
