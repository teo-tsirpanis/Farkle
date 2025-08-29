// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using Microsoft.CodeAnalysis;

namespace System.Collections.Generic
{
    [Embedded]
    internal static class DictionaryCompat
    {
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull
        {
            if (dict.ContainsKey(key))
            {
                return false;
            }
            dict.Add(key, value);
            return true;
        }
    }
}
#endif
