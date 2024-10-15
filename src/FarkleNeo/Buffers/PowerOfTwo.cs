// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Buffers;

/// <summary>
/// Represents a power of two and enables efficiently multiplying an integer by it.
/// </summary>
[DebuggerDisplay("{Value}")]
internal readonly struct PowerOfTwo
{
    public byte Log2 { get; private init; }

    public int Value => 1 << Log2;

    public static PowerOfTwo FromLog2(int valueLog2)
    {
        Debug.Assert(valueLog2 is >= 0 and <= 2);
        return new() { Log2 = (byte)valueLog2 };
    }

    public static int operator *(int idx, PowerOfTwo dataSize) => idx << dataSize.Log2;

    public static implicit operator int(PowerOfTwo dataSize) => dataSize.Value;
}
