// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Runtime.InteropServices;
using static Farkle.Grammars.GrammarConstants;

namespace Farkle.Grammars;

[StructLayout(LayoutKind.Auto)]
internal readonly struct GrammarHeader
{
    /// <summary>
    /// The size of the version-independent part of a Farkle grammar header.
    /// </summary>
    private const int VersionIndependentHeaderSize = sizeof(ulong) + 2 * sizeof(ushort);

    public GrammarHeader(ushort versionMajor, ushort versionMinor, uint streamCount, GrammarFileType fileType)
    {
        VersionMajor = versionMajor;
        VersionMinor = versionMinor;
        StreamCount = streamCount;
        FileType = fileType;
    }

    public ushort VersionMajor { get; private init; }
    public ushort VersionMinor { get; private init; }
    public uint StreamCount { get; private init; }
    public GrammarFileType FileType { get; private init; }

    public static GrammarHeader Unknown => default;
    public static GrammarHeader Cgt => new() { FileType = GrammarFileType.Cgt };
    public static GrammarHeader Egt5 => new() { FileType = GrammarFileType.Egt5 };
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

        if (grammarFile.StartsWith(Egt5Header))
        {
            return Egt5;
        }

        if (grammarFile.StartsWith(CgtHeader))
        {
            return Cgt;
        }

        return Unknown;
    }

    public bool IsSupported => (FileType, VersionMajor) is (GrammarFileType.Farkle, >= MinSupportedVersionMajor and <= GrammarConstants.VersionMajor);

    public bool HasUnknownData => (VersionMajor, VersionMinor) is not (GrammarConstants.VersionMajor, GrammarConstants.VersionMinor);
}
