// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETSTANDARD2_1_OR_GREATER || NET)
using System.Runtime.InteropServices;

namespace System.Text;

internal static class EncoderCompat
{
    public static unsafe int GetByteCount(this Encoder encoder, ReadOnlySpan<char> chars, bool flush)
    {
        char dummy = '\0';
        fixed (char* charsPtr = &(!chars.IsEmpty ? ref MemoryMarshal.GetReference(chars) : ref dummy))
        {
            return encoder.GetByteCount(charsPtr, chars.Length, flush);
        }
    }

    public static unsafe void Convert(this Encoder encoder, ReadOnlySpan<char> chars, Span<byte> bytes,
        bool flush, out int charsUsed, out int bytesUsed, out bool completed)
    {
        byte dummyByte = 0;
        char dummyChar = '\0';
        fixed (char* charsPtr = &(!chars.IsEmpty ? ref MemoryMarshal.GetReference(chars) : ref dummyChar))
        {
            fixed (byte* bytesPtr = &(!bytes.IsEmpty ? ref MemoryMarshal.GetReference(bytes) : ref dummyByte))
            {
                encoder.Convert(charsPtr, chars.Length, bytesPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
            }
        }
    }
}
#endif
