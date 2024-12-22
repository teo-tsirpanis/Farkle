// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    internal static class HashCodeCompat
    {
        public static void AddBytes(ref this HashCode hashCode, ReadOnlySpan<byte> value)
        {
            ref byte start = ref MemoryMarshal.GetReference(value);
            ref byte end = ref Unsafe.Add(ref start, value.Length);
            while ((nint)Unsafe.ByteOffset(ref start, ref end) >= sizeof(int))
            {
                hashCode.Add(Unsafe.ReadUnaligned<int>(ref start));
                start = ref Unsafe.Add(ref start, sizeof(int));
            }
            while (Unsafe.IsAddressLessThan(ref start, ref end))
            {
                hashCode.Add(start);
                start = ref Unsafe.Add(ref start, sizeof(int));
            }
        }
    }
}
#endif
