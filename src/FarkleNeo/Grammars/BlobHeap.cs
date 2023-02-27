// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars;

internal readonly struct BlobHeap
{
    private readonly int Offset, Length;

    public BlobHeap(GrammarFileSection section)
    {
        if ((uint)section.Length is not 0 and > GrammarConstants.MaxHeapSize)
        {
            ThrowHelpers.ThrowInvalidDataException("Blob heap is too large.");
        }

        Offset = section.Offset;
        Length = section.Length;
    }

    public GrammarFileSection GetBlobSection(ReadOnlySpan<byte> grammarFile, BlobHandle handle)
    {
        if (handle.IsEmpty)
        {
            return GrammarFileSection.Empty;
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

        return new GrammarFileSection(Offset + blobContentStart, blobLength);
    }
}
