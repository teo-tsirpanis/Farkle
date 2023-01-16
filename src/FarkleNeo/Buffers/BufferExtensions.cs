// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;

namespace Farkle.Buffers;

internal static class BufferExtensions
{
    // PERF: Skip bounds checks since we already do them, or confirm the JIT has eliminated them.
    public static int ReadInt32(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadInt32LittleEndian(buffer[index..]);

    public static ulong ReadUInt64(this ReadOnlySpan<byte> buffer, int index) =>
        BinaryPrimitives.ReadUInt64LittleEndian(buffer[index..]);
}
