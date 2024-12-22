// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle;

internal static class BitArithmetic
{
    public static int Align(int offset, int alignment)
    {
        Debug.Assert(IsPow2((uint)alignment));
        return (offset + alignment - 1) & ~(alignment - 1);
    }

    public static bool IsPow2(uint value) =>
        (value & (value - 1)) == 0 && value != 0;
}
