// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Farkle.Collections;

internal sealed class StringDictionary<TValue> : SpanDictionaryBase<char, string, TValue>
{
    protected override ReadOnlySpan<char> AsSpan(string container) => container.AsSpan();

    protected override int GetHashCode(ReadOnlySpan<char> key)
    {
#if NET6_0_OR_GREATER
        return string.GetHashCode(key);
#else
        HashCode hc = new();
        hc.AddBytes(MemoryMarshal.AsBytes(key));
        return hc.ToHashCode();
#endif
    }

    protected override string ToContainer(ReadOnlySpan<char> key) => key.ToString();
}
