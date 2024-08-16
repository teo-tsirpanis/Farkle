// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
using System.Runtime.InteropServices;

namespace System.Text;

internal static class EncodingCompat
{
    public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
    {
        char dummy = '\0';
        fixed (char* charsPtr = &(!chars.IsEmpty ? ref MemoryMarshal.GetReference(chars) : ref dummy))
        {
            return encoding.GetByteCount(charsPtr, chars.Length);
        }
    }

    public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        byte dummyByte = 0;
        char dummyChar = '\0';
        fixed (char* charsPtr = &(!chars.IsEmpty ? ref MemoryMarshal.GetReference(chars) : ref dummyChar))
        {
            fixed (byte* bytesPtr = &(!bytes.IsEmpty ? ref MemoryMarshal.GetReference(bytes) : ref dummyByte))
            {
                return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
            }
        }
    }

    public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        byte dummy = 0;
        fixed (byte* ptr = &(!bytes.IsEmpty ? ref MemoryMarshal.GetReference(bytes) : ref dummy))
        {
            return encoding.GetString(ptr, bytes.Length);
        }
    }
}
#endif
