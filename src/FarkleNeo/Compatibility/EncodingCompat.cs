// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETSTANDARD2_1_OR_GREATER || NET)
using System.Runtime.InteropServices;

namespace System.Text;

internal static class EncodingCompat
{
    public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
    {
        fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
        {
            return encoding.GetByteCount(charsPtr, chars.Length);
        }
    }

    public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
        {
            fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
            {
                return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
            }
        }
    }

    public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(bytes))
        {
            return encoding.GetString(ptr, bytes.Length);
        }
    }
}
#endif
