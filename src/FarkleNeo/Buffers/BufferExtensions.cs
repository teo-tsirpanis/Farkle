// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Buffers.Binary;

namespace Farkle.Buffers;

internal static class BufferExtensions
{
    // PERF: Skip bounds checks since we already do them, or confirm the JIT has eliminated them.
    public static int ReadInt32(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadInt32LittleEndian(buffer[index..]);

    public static ulong ReadUInt64(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadUInt64LittleEndian(buffer[index..]);

    public static void WriteBlobLength(this IBufferWriter<byte> buffer, int value)
    {
        switch ((uint)value)
        {
            case <= 0x7F:
                buffer.Write(value);
                break;
            case <= 0x3FFF:
                buffer.Write(BinaryPrimitives.ReverseEndianness((ushort)value | 0x8000));
                break;
            case <= 0x1FFFFFFF:
                buffer.Write(BinaryPrimitives.ReverseEndianness((uint)value | 0xC0000000));
                break;
            default:
                ThrowHelpers.ThrowBlobTooBig(value);
                break;
        }
    }

    public static void Write(this IBufferWriter<byte> buffer, byte value)
    {
        buffer.GetSpan()[0] = value;
        buffer.Advance(sizeof(byte));
    }

    public static void Write(this IBufferWriter<byte> buffer, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.GetSpan(sizeof(int)), value);
        buffer.Advance(sizeof(int));
    }

    public static void Write(this IBufferWriter<byte> buffer, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.GetSpan(sizeof(ulong)), value);
        buffer.Advance(sizeof(ulong));
    }
}
