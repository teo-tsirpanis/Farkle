// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using Microsoft.CodeAnalysis;

namespace System;

[Embedded]
internal struct HashCode
{
    private const int FnvOffsetBasis = unchecked((int)2166136261);

    private const int FnvPrime = 16777619;

    private int _hash = FnvOffsetBasis;

    public HashCode() { }

    public void Add<T>(T item)
    {
        _hash ^= item?.GetHashCode() ?? 0;
        _hash *= FnvPrime;
    }

    public readonly int ToHashCode() => _hash;
}
#endif
