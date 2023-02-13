// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;

namespace Farkle.Grammars;

internal readonly struct BlobHeap
{
    private readonly int Offset, Length;

    public BlobHeap(int blobHeapOffset, int blobHeapLength)
    {
        Debug.Assert(blobHeapOffset >= 0);
        Debug.Assert(blobHeapLength >= 0);
        if (blobHeapLength != 0)
        {
            if ((uint)blobHeapLength > GrammarConstants.MaxHeapSize)
            {
                ThrowHelpers.ThrowInvalidDataException("Blob heap is too large.");
            }
        }

        Offset = blobHeapOffset;
        Length = blobHeapLength;
    }

    public (int Offset, int Length) GetBlobAbsoluteBounds(ReadOnlySpan<byte> grammarFile, BlobHandle handle)
    {
        if (handle.IsEmpty)
        {
            return (0, 0);
        }

        if (handle.Value >= (uint)Length)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Handle points outside the blob heap.");
        }

        int blobStart = (int)handle.Value;
        int blobLength = grammarFile.ReadBlobLength(Offset + blobStart, out int blobContentStartOffset);
        int blobContentStart = blobStart += blobContentStartOffset;
        if (BufferExtensions.IsOutOfBounds(blobContentStart, blobLength, Length))
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle), "Invalid handle.");
        }

        return (Offset + blobContentStart, blobLength);
    }
}
