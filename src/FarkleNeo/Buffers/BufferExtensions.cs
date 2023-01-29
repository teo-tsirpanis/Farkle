// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Buffers.Binary;

namespace Farkle.Buffers;

internal static class BufferExtensions
{
    public static bool IsOutOfBounds(int offset, int length, int fileLength)
    {
        // https://github.com/dotnet/runtime/blob/ae6a73f82ff572004d739083751026d0b82663f3/src/libraries/System.Private.CoreLib/src/System/Span.cs#L416-L428
        if (IntPtr.Size == 8)
        {
            return (uint)offset + (ulong)(uint)length > (uint)fileLength;
        }

        return (uint)offset > (uint)fileLength || (uint)length > (uint)(fileLength - offset);
    }

    // PERF: Skip bounds checks since we already do them, or confirm the JIT has eliminated them.
    public static int ReadBlobLength(this ReadOnlySpan<byte> buffer, int index, out int bytesRead)
    {
        uint first = buffer[index];
        if (first <= 0x7F)
        {
            bytesRead = 1;
            return (int)first;
        }
        if ((first & 0xC0) == 0x80)
        {
            bytesRead = 2;
            return BinaryPrimitives.ReverseEndianness(buffer.ReadUInt16(index)) & 0x3FFF;
        }
        if ((first & 0xE0) == 0xC0)
        {
            bytesRead = 4;
            return (int)(BinaryPrimitives.ReverseEndianness(buffer.ReadUInt32(index)) & 0x1FFFFFFF);
        }

        ThrowHelpers.ThrowInvalidDataException("Invalid blob length");
        bytesRead = 0;
        return 0;
    }

    public static int ReadInt32(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadInt32LittleEndian(buffer[index..]);

    public static ushort ReadUInt16(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(buffer[index..]);

    public static uint ReadUInt32(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer[index..]);

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

    public static void Write(this IBufferWriter<byte> buffer, byte value, int count)
    {
        while (count > 0)
        {
            Span<byte> span = buffer.GetSpan();
            int batchCount = Math.Min(count, span.Length);
            span[..batchCount].Fill(value);
            buffer.Advance(batchCount);
            count -= batchCount;
        }
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
