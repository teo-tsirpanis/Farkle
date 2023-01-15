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
    internal static GrammarHeader ReadHeader(ref BufferReader reader)
    {
        if (reader.PeekUInt64() == HeaderMagic)
        {
            reader.AdvanceBy(sizeof(ulong));
            ushort versionMajor = reader.ReadUInt16();
            ushort versionMinor = reader.ReadUInt16();

            if (versionMajor is > VersionMajor or < MinSupportedVersionMajor)
            {
                return GrammarHeader.CreateFarkle(versionMajor, versionMinor, 0);
            }

            uint streamCount = reader.ReadUInt32();
            return GrammarHeader.CreateFarkle(versionMajor, versionMinor, streamCount);
        }

        ReadOnlySpan<byte> buffer = reader.RemainingBuffer;

        if (buffer.StartsWith(EgtNeoHeader))
        {
            return GrammarHeader.EgtNeo;
        }

        if (buffer.StartsWith(Egt5Header))
        {
            return GrammarHeader.Egt5;
        }

        if (buffer.StartsWith(CgtHeader))
        {
            return GrammarHeader.Cgt;
        }

        return GrammarHeader.Unknown;
    }
}
