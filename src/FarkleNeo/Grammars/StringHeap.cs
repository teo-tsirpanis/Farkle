// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Farkle.Grammars;

internal readonly struct StringHeap
{
    private readonly int Offset, Length;

    public StringHeap(ReadOnlySpan<byte> grammarFile, int stringHeapOffset, int stringHeapLength)
    {
        Debug.Assert(stringHeapOffset >= 0);
        Debug.Assert(stringHeapLength >= 0);
        if (stringHeapLength != 0)
        {
            ReadOnlySpan<byte> stringHeap = grammarFile.Slice(stringHeapOffset, stringHeapLength);
            if (stringHeap[0] != 0)
            {
                ThrowHelpers.ThrowInvalidDataException("String heap does not start with null byte.");
            }

            if (stringHeap[^1] != 0)
            {
                ThrowHelpers.ThrowInvalidDataException("String heap does not end with null byte.");
            }

            // It would be nice if Encoding.GetCharCount reported errors;
            // we could validate that the string heap's content is valid UTF-8.
            // We still could do it efficiently with a dummy IBufferWriter.
        }

        Offset = stringHeapOffset;
        Length = stringHeapLength;
    }

    public string GetString(ReadOnlySpan<byte> grammarFile, StringHandle handle)
    {
        if (handle.IsNil)
        {
            return string.Empty;
        }

        if (handle.Value >= (uint)Length)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Handle points outside the string heap.");
        }

        ReadOnlySpan<byte> stringHeap = grammarFile.Slice(Offset, Length);
        int stringStart = (int)handle.Value;
        if (stringHeap[stringStart - 1] != 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Handle cannot point to the middle of a string.");
        }

        int stringLength = stringHeap[stringStart..].IndexOf((byte)0);
        Debug.Assert(stringLength >= 0);
        ReadOnlySpan<byte> stringContent = stringHeap.Slice(stringStart, stringLength);
        return Encoding.UTF8.GetString(stringContent);
    }

    public StringHandle LookupString(ReadOnlySpan<byte> grammarFile, ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty || Length == 0)
        {
            return default;
        }

        if (chars.IndexOf('\0') != -1)
        {
            return default;
        }

        const int StackAllocationSize = 64;
        byte[]? pooledArray = null;
        try
        {
            scoped Span<byte> buffer;
            int maxByteCount = Utf8EncodingStrict.Instance.GetMaxByteCount(chars.Length);
            if (maxByteCount < StackAllocationSize - 2)
            {
                buffer = stackalloc byte[StackAllocationSize];
            }
            else
            {
                buffer = pooledArray = ArrayPool<byte>.Shared.Rent(maxByteCount + 2);
            }

            int writtenBytes;
            try
            {
                writtenBytes = Utf8EncodingStrict.Instance.GetBytes(chars, buffer[1..^1]);
            }
            catch (EncoderFallbackException)
            {
                return default;
            }

            Span<byte> stringBytes = buffer[..(writtenBytes + 2)];
            stringBytes[0] = stringBytes[^1] = 0;

            // stringBytes contains "\0mystring\0", and we can IndexOf it in the heap.
            int locationInStringHeap = grammarFile.Slice(Offset, Length).IndexOf(stringBytes);
            if (locationInStringHeap < 0)
            {
                return default;
            }
            return new StringHandle((uint)locationInStringHeap + 1);
        }
        finally
        {
            if (pooledArray is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }
}
