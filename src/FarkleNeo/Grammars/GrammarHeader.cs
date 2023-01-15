// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Farkle.Grammars;

[StructLayout(LayoutKind.Auto)]
internal readonly struct GrammarHeader
{
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

    public bool IsSupported => (FileType, VersionMajor) is (GrammarFileType.Farkle, >= GrammarConstants.MinSupportedVersionMajor and <= GrammarConstants.VersionMajor);

    public bool HasUnknownData => (VersionMajor, VersionMinor) is not (GrammarConstants.VersionMajor, GrammarConstants.VersionMinor);
}
