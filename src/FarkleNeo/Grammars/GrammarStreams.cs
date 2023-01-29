// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars;

internal readonly struct GrammarStreams
{
    public readonly int StringHeapOffset, StringHeapLength;
    public readonly int BlobHeapOffset, BlobHeapLength;
    public readonly int TableStreamOffset, TableStreamLength;

    public GrammarStreams(ref BufferReader br, uint streamCount, out bool hasUnknownStreams)
    {
        int fileLength = br.OriginalBuffer.Length;
        bool seenStringHeap = false, seenBlobHeap = false, seenTableStream = false;
        hasUnknownStreams = false;

        for (uint i = 0; i < streamCount; i++)
        {
            ulong identifier = br.ReadUInt64();
            int offset = br.ReadInt32();
            int length = br.ReadInt32();
            if (BufferExtensions.IsOutOfBounds(offset, length, fileLength))
            {
                ThrowHelpers.ThrowInvalidDataException("Invalid stream bounds.");
            }
            switch (identifier)
            {
                case GrammarConstants.StringHeapIdentifier:
                    AssignStream(identifier, offset, length, ref StringHeapOffset, ref StringHeapLength, ref seenStringHeap);
                    break;
                case GrammarConstants.BlobHeapIdentifier:
                    AssignStream(identifier, offset, length, ref BlobHeapOffset, ref BlobHeapLength, ref seenBlobHeap);
                    break;
                case GrammarConstants.TableStreamIdentifier:
                    AssignStream(identifier, offset, length, ref TableStreamOffset, ref TableStreamLength, ref seenTableStream);
                    break;
                default:
                    // We could have detected duplicate unknown streams to fully conform
                    // with the spec, but let's not, we don't care about them. We do however check their bounds.
                    hasUnknownStreams = true;
                    break;
            }
        }

        if (!seenTableStream)
            ThrowHelpers.ThrowInvalidDataException("Missing table stream.");
    }

    private static void AssignStream(ulong identifier, int offset, int length, ref int offsetAddress, ref int lengthAddress, ref bool seen)
    {
        if (seen)
        {
            ThrowDuplicateStream(identifier);
        }
        seen = true;
        offsetAddress = offset;
        lengthAddress = length;

        static void ThrowDuplicateStream(ulong identifier) =>
            ThrowHelpers.ThrowInvalidDataException($"Duplicate stream {identifier:X8}.");
    }
}
