// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Farkle.Grammars;

internal readonly struct StringHeap
{
    private readonly int Offset, Length;

    public StringHeap(ReadOnlySpan<byte> grammarFile, GrammarFileSection section)
    {
        Offset = section.Offset;
        Length = section.Length;
        if (Length != 0)
        {
            if (grammarFile[Offset] != 0)
            {
                ThrowHelpers.ThrowInvalidDataException("String heap does not start with null byte.");
            }

            if (grammarFile[Offset + Length - 1] != 0)
            {
                ThrowHelpers.ThrowInvalidDataException("String heap does not end with null byte.");
            }

            if ((uint)Length > GrammarConstants.MaxHeapSize)
            {
                ThrowHelpers.ThrowInvalidDataException("String heap is too large.");
            }

            // It would be nice if Encoding.GetCharCount reported errors;
            // we could validate that the string heap's content is valid UTF-8.
            // We still could do it efficiently with a dummy IBufferWriter.
        }
    }

    public GrammarFileSection GetStringSection(ReadOnlySpan<byte> grammarFile, StringHandle handle)
    {
        if (handle.IsEmpty)
        {
            return GrammarFileSection.Empty;
        }

        if (handle.Value >= (uint)Length)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Handle cannot point outside the string heap.");
        }

        int stringStart = (int)handle.Value;
        if (grammarFile[Offset + stringStart - 1] != 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Handle cannot point to the middle of a string.");
        }

        int stringLength = grammarFile[(Offset + stringStart)..].IndexOf((byte)0);
        Debug.Assert(stringLength >= 0);
        return new GrammarFileSection(Offset + stringStart, stringLength);
    }

    public string GetString(ReadOnlySpan<byte> grammarFile, StringHandle handle)
    {
        GrammarFileSection section = GetStringSection(grammarFile, handle);
        return Encoding.UTF8.GetString(grammarFile.Slice(section.Offset, section.Length));
    }

    public StringHandle? LookupString(ReadOnlySpan<byte> grammarFile, ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty || Length == 0)
        {
            return default(StringHandle);
        }

        if (chars.IndexOf('\0') != -1)
        {
            return null;
        }

        const int StackAllocationSize = 64;
        byte[]? pooledArray = null;
        try
        {
            scoped Span<byte> buffer;
            int maxByteCount = Encoding.UTF8.GetMaxByteCount(chars.Length);
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
                writtenBytes = Encoding.UTF8.GetBytes(chars, buffer[1..^1]);
            }
            catch (EncoderFallbackException)
            {
                return null;
            }

            Span<byte> stringBytes = buffer[..(writtenBytes + 2)];
            stringBytes[0] = stringBytes[^1] = 0;

            // stringBytes contains "\0mystring\0", and we can IndexOf it in the heap.
            int locationInStringHeap = grammarFile.Slice(Offset, Length).IndexOf(stringBytes);
            if (locationInStringHeap < 0)
            {
                return null;
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
