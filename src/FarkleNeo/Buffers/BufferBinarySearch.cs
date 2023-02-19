// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Buffers;

internal static class BufferBinarySearch
{
    public static unsafe int SearchChar(ReadOnlySpan<byte> buffer, int @base, int length, char item)
    {
        int low = @base, high = @base + length * sizeof(char);

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            char midItem = (char)buffer.ReadUInt16(mid);

            if (midItem == item)
            {
                return mid;
            }
            else if (midItem < item)
            {
                low = mid + sizeof(char);
            }
            else
            {
                high = mid - sizeof(char);
            }
        }

        return ~low;
    }
}
