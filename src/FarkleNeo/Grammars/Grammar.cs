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
    /// <summary>
    /// The size of the version-independent part of a Farkle grammar header.
    /// </summary>
    private const int VersionIndependentHeaderSize = sizeof(ulong) + 2 * sizeof(ushort);

    internal static GrammarHeader ReadHeader(ReadOnlySpan<byte> grammarFile)
    {
        if (grammarFile.Length >= VersionIndependentHeaderSize && grammarFile.ReadUInt64(0) == HeaderMagic)
        {
            ushort versionMajor = grammarFile.ReadUInt16(sizeof(ulong));
            ushort versionMinor = grammarFile.ReadUInt16(sizeof(ulong) + sizeof(ushort));

            if (versionMajor is > VersionMajor or < MinSupportedVersionMajor
                || grammarFile.Length < VersionIndependentHeaderSize + sizeof(uint))
            {
                return GrammarHeader.CreateFarkle(versionMajor, versionMinor, 0);
            }

            uint streamCount = grammarFile.ReadUInt32(VersionIndependentHeaderSize);
            return GrammarHeader.CreateFarkle(versionMajor, versionMinor, streamCount);
        }

        if (grammarFile.StartsWith(EgtNeoHeader))
        {
            return GrammarHeader.EgtNeo;
        }

        if (grammarFile.StartsWith(Egt5Header))
        {
            return GrammarHeader.Egt5;
        }

        if (grammarFile.StartsWith(CgtHeader))
        {
            return GrammarHeader.Cgt;
        }

        return GrammarHeader.Unknown;
    }
}
