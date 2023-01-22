// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETSTANDARD2_1_OR_GREATER || NET)
using System.Runtime.InteropServices;

namespace System.Text;

internal static class EncoderCompat
{
    public static unsafe int GetByteCount(this Encoder encoder, ReadOnlySpan<char> chars, bool flush)
    {
        fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
        {
            return encoder.GetByteCount(charsPtr, chars.Length, flush);
        }
    }

    public static unsafe void Convert(this Encoder encoder, ReadOnlySpan<char> chars, Span<byte> bytes,
        bool flush, out int charsUsed, out int bytesUsed, out bool completed)
    {
        fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
        {
            fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
            {
                encoder.Convert(charsPtr, chars.Length, bytesPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
            }
        }
    }
}
#endif
