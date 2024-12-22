// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using System.Runtime.InteropServices;

namespace System.Text;

internal static class StringBuilderCompat
{
    public static unsafe StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        fixed (char* ptr = &MemoryMarshal.GetReference(value))
        {
            return sb.Append(ptr, value.Length);
        }
    }
}
#endif
