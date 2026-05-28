// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Collections;

internal sealed class StringDictionary<TValue> : SpanDictionaryBase<char, string, TValue>
{
    protected override ReadOnlySpan<char> AsSpan(string container) => container;

    protected override int GetHashCode(ReadOnlySpan<char> key) => string.GetHashCode(key);

    protected override string ToContainer(ReadOnlySpan<char> key) => key.ToString();
}
