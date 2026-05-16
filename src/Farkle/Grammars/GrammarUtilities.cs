// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars
{
    internal static class GrammarUtilities
    {
        /// <summary>
        /// Gets the size in bytes of a compressed index to a collection of objects in a grammar.
        /// </summary>
        /// <param name="objectCount">The number of objects in the collection.</param>
        public static byte GetCompressedIndexSize(int objectCount) => objectCount switch
        {
            < byte.MaxValue => sizeof(byte),
            < ushort.MaxValue => sizeof(ushort),
            _ => sizeof(uint)
        };

        /// <summary>
        /// Gets a bitmask for the valid bits of a compressed index of the given size.
        /// </summary>
        /// <param name="indexSize">The size of the compressed index in bytes.</param>
        public static uint GetMaskForCompressedIndexSize(byte indexSize) => (1u << (indexSize * 8)) - 1;

        /// <summary>
        /// Gets the size in bytes of a coded index to two tables.
        /// </summary>
        public static byte GetBinaryCodedIndexSize(int row1Count, int row2Count) => (row1Count | row2Count) switch
        {
            <= sbyte.MaxValue => sizeof(sbyte),
            <= short.MaxValue => sizeof(short),
            _ => sizeof(int)
        };

        /// <summary>
        /// Gets the size in bytes of the encoded representation of an <see cref="StateMachines.LrAction"/>.
        /// </summary>
        /// <param name="stateCount">The number of LR(0) states in the grammar.</param>
        /// <param name="productionCount">The number of productions in the grammar.</param>
        public static byte GetLrActionEncodedSize(int stateCount, int productionCount) => (stateCount, productionCount) switch
        {
            (<= sbyte.MaxValue - 1, <= -sbyte.MinValue) => sizeof(sbyte),
            (<= short.MaxValue - 1, <= -short.MinValue) => sizeof(short),
            _ => sizeof(int)
        };
    }
}
