// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

internal static class GrammarConstants
{
    public const ulong HeaderMagic = 0x0000656C6B726146; // "Farkle\0\0"
    public const ulong StringHeapIdentifier = 0x73676E6972745323; // "#Strings"
    public const ulong BlobHeapIdentifier = 0x000000646F6C4223; // "#Blob\0\0\0"
    public const ulong TableStreamIdentifier = 0x0000000000007E23; // "#~\0\0\0\0\0\0"

    public const uint MaxHeapSize = (1 << 29) - 1;

    public const ushort VersionMajor = 7;
    public const ushort VersionMinor = 0;

    public const ushort MinSupportedVersionMajor = 7;

    public const uint DfaOnCharKind = 0;
    public const uint DfaOnCharWithConflictsKind = 1;
    public const uint DfaOnCharDefaultTransitionsKind = 2;

    public const uint Lr1Kind = 3;
    public const uint Glr1Kind = 4;

    public static ReadOnlySpan<byte> CgtHeader => "G\0O\0L\0D\0 \0P\0a\0r\0s\0e\0r\0 \0T\0a\0b\0l\0e\0s\0/\0v\01\0.\00\0\0\0"u8;
    public static ReadOnlySpan<byte> Egt5Header => "G\0O\0L\0D\0 \0P\0a\0r\0s\0e\0r\0 \0T\0a\0b\0l\0e\0s\0/\0v\05\0.\00\0\0\0"u8;
    public static ReadOnlySpan<byte> EgtNeoHeader => "F\0a\0r\0k\0l\0e\0 \0P\0a\0r\0s\0e\0r\0 \0T\0a\0b\0l\0e\0s\0/\0v\06\0.\00\0\0\0"u8;

    public const string HeaderMagicString = "\u6146\u6b72\u656c";
    public const string CgtHeaderString = "GOLD Parser Tables/v1.0";
    public const string Egt5HeaderString = "GOLD Parser Tables/v5.0";
    public const string EgtNeoHeaderString = "Farkle Parser Tables/v6.0";
}
