// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Farkle.Grammars.GrammarConstants;

namespace Farkle.Grammars;

[StructLayout(LayoutKind.Auto)]
internal readonly struct GrammarHeader(ushort versionMajor, ushort versionMinor, uint streamCount, GrammarFileType fileType)
{
    /// <summary>
    /// The size of the version-independent part of a Farkle grammar header.
    /// </summary>
    private const int VersionIndependentHeaderSize = sizeof(ulong) + 2 * sizeof(ushort);

    /// <summary>
    /// The smallest number of bytes necessary to read from
    /// the start of a Farkle grammar file to determine its type.
    /// </summary>
    public static int MinHeaderDisambiguatorSize => EgtNeoHeader.Length;

#if DEBUG
    static GrammarHeader()
    {
        Debug.Assert(MinHeaderDisambiguatorSize >= VersionIndependentHeaderSize);
        Debug.Assert(MinHeaderDisambiguatorSize >= CgtHeader.Length);
        Debug.Assert(MinHeaderDisambiguatorSize >= EgtHeader.Length);
        Debug.Assert(MinHeaderDisambiguatorSize >= EgtNeoHeader.Length);
    }
#endif

    public ushort VersionMajor { get; private init; } = versionMajor;
    public ushort VersionMinor { get; private init; } = versionMinor;
    public uint StreamCount { get; private init; } = streamCount;
    public GrammarFileType FileType { get; private init; } = fileType;

    public static GrammarHeader Unknown => default;
    public static GrammarHeader GoldParser => new() { FileType = GrammarFileType.GoldParser };
    public static GrammarHeader EgtNeo => new() { FileType = GrammarFileType.EgtNeo };
    public static GrammarHeader CreateFarkle(ushort versionMajor, ushort versionMinor, uint streamCount) =>
        new(versionMajor, versionMinor, streamCount, GrammarFileType.Farkle);

    public static GrammarHeader Read(ReadOnlySpan<byte> grammarFile)
    {
        if (grammarFile.Length >= VersionIndependentHeaderSize && grammarFile.ReadUInt64(0) == HeaderMagic)
        {
            ushort versionMajor = grammarFile.ReadUInt16(sizeof(ulong));
            ushort versionMinor = grammarFile.ReadUInt16(sizeof(ulong) + sizeof(ushort));

            if (versionMajor is > GrammarConstants.VersionMajor or < MinSupportedVersionMajor
                || grammarFile.Length < VersionIndependentHeaderSize + sizeof(uint))
            {
                return CreateFarkle(versionMajor, versionMinor, 0);
            }

            uint streamCount = grammarFile.ReadUInt32(VersionIndependentHeaderSize);
            return CreateFarkle(versionMajor, versionMinor, streamCount);
        }

        if (grammarFile.StartsWith(EgtNeoHeader))
        {
            return EgtNeo;
        }

        if (grammarFile.StartsWith(EgtHeader) || grammarFile.StartsWith(CgtHeader))
        {
            return GoldParser;
        }

        return Unknown;
    }

    public bool IsSupported => (FileType, VersionMajor) is (GrammarFileType.Farkle, >= MinSupportedVersionMajor and <= GrammarConstants.VersionMajor);

    public bool HasUnknownData => (VersionMajor, VersionMinor) is not (GrammarConstants.VersionMajor, GrammarConstants.VersionMinor);
}
