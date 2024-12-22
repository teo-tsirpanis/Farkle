// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Collections;

internal sealed class BlobDictionary<TValue> : SpanDictionaryBase<byte, ImmutableArray<byte>, TValue>
{
    protected override ImmutableArray<byte> ToContainer(ReadOnlySpan<byte> key) => key.ToImmutableArray();

    protected override int GetHashCode(ReadOnlySpan<byte> key)
    {
        HashCode hc = new();
        hc.AddBytes(key);
        return hc.ToHashCode();
    }

    protected override ReadOnlySpan<byte> AsSpan(ImmutableArray<byte> container) => container.AsSpan();
}
