// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Grammars;

internal static class GrammarUtilities
{
    /// <summary>
    /// Gets the size in bytes of a compressed index to a collection of objects in a grammar.
    /// </summary>
    /// <param name="objectCount">The number of objects in the collection.</param>
    public static PowerOfTwo GetCompressedIndexSize(int objectCount) => objectCount switch
    {
        < byte.MaxValue => PowerOfTwo.FromLog2(0),
        < ushort.MaxValue => PowerOfTwo.FromLog2(1),
        _ => PowerOfTwo.FromLog2(2)
    };

    /// <summary>
    /// Gets the size in bytes of a coded index to two tables.
    /// </summary>
    public static PowerOfTwo GetBinaryCodedIndexSize(int row1Count, int row2Count) => (row1Count | row2Count) switch
    {
        <= sbyte.MaxValue => PowerOfTwo.FromLog2(0),
        <= short.MaxValue => PowerOfTwo.FromLog2(1),
        _ => PowerOfTwo.FromLog2(2)
    };

    /// <summary>
    /// Gets the size in bytes of the encoded representation of an <see cref="StateMachines.LrAction"/>.
    /// </summary>
    /// <param name="stateCount">The number of LR(0) states in the grammar.</param>
    /// <param name="productionCount">The number of productions in the grammar.</param>
    public static PowerOfTwo GetLrActionEncodedSize(int stateCount, int productionCount) => (stateCount, productionCount) switch
    {
        (<= sbyte.MaxValue - 1, <= -sbyte.MinValue) => PowerOfTwo.FromLog2(0),
        (<= short.MaxValue - 1, <= -short.MinValue) => PowerOfTwo.FromLog2(1),
        _ => PowerOfTwo.FromLog2(2)
    };

    public static PowerOfTwo GetStringHeapIndexSize(GrammarHeapSizes heapSizes) =>
        PowerOfTwo.FromLog2((heapSizes & GrammarHeapSizes.StringHeapSmall) != 0 ? 1 : 2);

    public static PowerOfTwo GetBlobHeapIndexSize(GrammarHeapSizes heapSizes) =>
        PowerOfTwo.FromLog2((heapSizes & GrammarHeapSizes.BlobHeapSmall) != 0 ? 1 : 2);
}
