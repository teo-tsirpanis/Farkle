// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Diagnostics;

namespace Farkle.Buffers;

internal ref struct BufferReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    public readonly ReadOnlySpan<byte> Buffer => _buffer[Position..];

    public int Position { get; private set; }

    public BufferReader(ReadOnlySpan<byte> span) => _buffer = span;

    public readonly ReadOnlySpan<byte> PeekBytes(int count)
    {
        Debug.Assert(count >= 0);
        if (Position - _buffer.Length + count is int remaining && remaining < 0)
        {
            count += remaining;
        }

        return _buffer[Position..count];
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        ReadOnlySpan<byte> result = PeekBytes(count);
        Position += result.Length;

        return result;
    }

    public void AdvanceBy(int byteCount)
    {
        Debug.Assert(byteCount >= 0);

        if (Position + byteCount >= _buffer.Length)
        {
            ThrowHelpers.ThrowInvalidOperationException($"Cannot advance buffer by {byteCount} bytes.");
        }

        Position += byteCount;
    }

    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(sizeof(short)));

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)));

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)));

    public bool TryReadInt16(out short value) => BinaryPrimitives.TryReadInt16BigEndian(ReadBytes(sizeof(short)), out value);

    public bool TryReadUInt16(out ushort value) => BinaryPrimitives.TryReadUInt16BigEndian(ReadBytes(sizeof(ushort)), out value);

    public bool TryReadInt32(out int value) => BinaryPrimitives.TryReadInt32BigEndian(ReadBytes(sizeof(int)), out value);

    public bool TryReadUInt32(out uint value) => BinaryPrimitives.TryReadUInt32BigEndian(ReadBytes(sizeof(uint)), out value);

    public bool TryReadInt64(out long value) => BinaryPrimitives.TryReadInt64BigEndian(ReadBytes(sizeof(long)), out value);

    public bool TryReadUInt64(out ulong value) => BinaryPrimitives.TryReadUInt64BigEndian(ReadBytes(sizeof(ulong)), out value);

    public readonly short PeekInt16() => BinaryPrimitives.ReadInt16LittleEndian(PeekBytes(sizeof(short)));

    public readonly ushort PeekUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(PeekBytes(sizeof(ushort)));

    public readonly int PeekInt32() => BinaryPrimitives.ReadInt32LittleEndian(PeekBytes(sizeof(int)));

    public readonly uint PeekUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(PeekBytes(sizeof(uint)));

    public readonly long PeekInt64() => BinaryPrimitives.ReadInt64LittleEndian(PeekBytes(sizeof(long)));

    public readonly ulong PeekUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(PeekBytes(sizeof(ulong)));

    public readonly bool TryPeekInt16(out short value) => BinaryPrimitives.TryReadInt16BigEndian(PeekBytes(sizeof(short)), out value);

    public readonly bool TryPeekUInt16(out ushort value) => BinaryPrimitives.TryReadUInt16BigEndian(PeekBytes(sizeof(ushort)), out value);

    public readonly bool TryPeekInt32(out int value) => BinaryPrimitives.TryReadInt32BigEndian(PeekBytes(sizeof(int)), out value);

    public readonly bool TryPeekUInt32(out uint value) => BinaryPrimitives.TryReadUInt32BigEndian(PeekBytes(sizeof(uint)), out value);

    public readonly bool TryPeekInt64(out long value) => BinaryPrimitives.TryReadInt64BigEndian(PeekBytes(sizeof(long)), out value);

    public readonly bool TryPeekUInt64(out ulong value) => BinaryPrimitives.TryReadUInt64BigEndian(PeekBytes(sizeof(ulong)), out value);
}
