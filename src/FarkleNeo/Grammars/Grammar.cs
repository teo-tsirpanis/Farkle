// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using static Farkle.Grammars.GrammarConstants;

namespace Farkle.Grammars;

/// <summary>
/// Represents a grammar file.
/// </summary>
public abstract class Grammar
{
    private static (ushort VersionMajor, ushort VersionMinor, uint StreamCount) ReadHeader(ref BufferReader reader)
    {
        if (reader.PeekUInt64() == HeaderMagic)
        {
            reader.AdvanceBy(sizeof(ulong));
            ushort versionMajor = reader.ReadUInt16();
            ushort versionMinor = reader.ReadUInt16();

            if (versionMajor is > VersionMajor or < MinSupportedVersionMajor)
            {
                ThrowHelpers.ThrowNotSupportedException($"Unsupported grammar format version.");
            }

            uint streamCount = reader.ReadUInt32();
            return (versionMajor, versionMinor, streamCount);
        }

        ReadOnlySpan<byte> buffer = reader.Buffer;

        if (buffer.SequenceEqual(EgtNeoHeader))
        {
            ThrowHelpers.ThrowNotSupportedException("Reading grammar files in the EGTneo format produced by Farkle 6 is not supported.");
        }

        if (buffer.SequenceEqual(Egt5Header))
        {
            // TODO: Add support.
            ThrowHelpers.ThrowNotSupportedException("Reading Enhanced Grammar Tables produced by GOLD Parser 5.x is not supported.");
        }

        if (buffer.SequenceEqual(CgtHeader))
        {
            ThrowHelpers.ThrowNotSupportedException("Reading Compiled Grammar Tables produced by earlier GOLD Parser versions is not supported.");
        }

        return default;
    }
}
