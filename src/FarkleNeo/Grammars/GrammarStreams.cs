// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars;

internal readonly struct GrammarStreams
{
    public readonly GrammarFileSection StringHeap, BlobHeap, TableStream;

    private const int StreamDefinitionsOffset =
        // Magic
        sizeof(ulong)
        // MajorVersion
        + sizeof(ushort)
        // MinorVersion
        + sizeof(ushort)
        // StreamCount
        + sizeof(int);

    private const int StreamDefinitionSize =
        // Identifier
        sizeof(ulong)
        // Offset
        + sizeof(int)
        // Length
        + sizeof(int);

    public GrammarStreams(ReadOnlySpan<byte> grammarFile, uint streamCount, out bool hasUnknownStreams)
    {
        bool seenStringHeap = false, seenBlobHeap = false, seenTableStream = false;
        hasUnknownStreams = false;

        if ((uint)grammarFile.Length < StreamDefinitionsOffset + streamCount * StreamDefinitionSize)
        {
            ThrowHelpers.ThrowInvalidDataException("Grammar header is too small.");
        }

        for (int i = 0; i < (int)streamCount; i++)
        {
            ulong identifier = grammarFile.ReadUInt64(StreamDefinitionsOffset + StreamDefinitionSize * i + 0);
            int offset = grammarFile.ReadInt32(StreamDefinitionsOffset + StreamDefinitionSize * i + sizeof(ulong));
            int length = grammarFile.ReadInt32(StreamDefinitionsOffset + StreamDefinitionSize * i + sizeof(ulong) + sizeof(int));
            if (BufferExtensions.IsOutOfBounds(offset, length, grammarFile.Length))
            {
                ThrowHelpers.ThrowInvalidDataException("Invalid stream bounds.");
            }
            GrammarFileSection section = new GrammarFileSection(offset, length);
            switch (identifier)
            {
                case GrammarConstants.StringHeapIdentifier:
                    AssignStream(identifier, section, ref StringHeap, ref seenStringHeap);
                    break;
                case GrammarConstants.BlobHeapIdentifier:
                    AssignStream(identifier, section, ref BlobHeap, ref seenBlobHeap);
                    break;
                case GrammarConstants.TableStreamIdentifier:
                    AssignStream(identifier, section, ref TableStream, ref seenTableStream);
                    break;
                default:
                    // We could have detected duplicate unknown streams to fully conform with the spec,
                    // but let's not, we don't care about them. We do however check their bounds.
                    hasUnknownStreams = true;
                    break;
            }
        }

        if (!seenTableStream)
            ThrowHelpers.ThrowInvalidDataException("Missing table stream.");
    }

    private static void AssignStream(ulong identifier, GrammarFileSection section, ref GrammarFileSection sectionAddress, ref bool seen)
    {
        if (seen)
        {
            ThrowDuplicateStream(identifier);
        }
        seen = true;
        sectionAddress = section;

        static void ThrowDuplicateStream(ulong identifier) =>
            ThrowHelpers.ThrowInvalidDataException($"Duplicate stream {identifier:X8}.");
    }
}
