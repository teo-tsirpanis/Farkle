// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NETCOREAPP3_0_OR_GREATER
using Microsoft.CodeAnalysis;

namespace System;

[Embedded]
internal static class MemoryExtensionsCompat
{
    public static bool Contains(this ReadOnlySpan<char> memory, char value) =>
        memory.IndexOf(value) != -1;
}
#endif
