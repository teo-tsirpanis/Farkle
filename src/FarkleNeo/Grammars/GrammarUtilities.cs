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

        public static byte GetDfaIndexSize(int stateCount, int edgeCount, int conflictAcceptCount, int tokenSymbolCount) =>
            Math.Max(GetCompressedIndexSize(stateCount),
                Math.Max(GetCompressedIndexSize(edgeCount),
                Math.Max(GetCompressedIndexSize(conflictAcceptCount),
                GetCompressedIndexSize(tokenSymbolCount))));

        public static byte GetLrIndexSize(int stateCount, int actionCount, int gotoCount, int conflictEofActionCount, int tokenSymbolCount, int nonterminalCount, int productionCount) =>
            Math.Max(GetCompressedIndexSize(stateCount),
                Math.Max(GetCompressedIndexSize(actionCount),
                Math.Max(GetCompressedIndexSize(gotoCount),
                Math.Max(GetCompressedIndexSize(conflictEofActionCount),
                Math.Max(GetLrActionEncodedSize(stateCount, productionCount),
                Math.Max(GetCompressedIndexSize(tokenSymbolCount),
                Math.Max(GetCompressedIndexSize(nonterminalCount),
                GetCompressedIndexSize(productionCount))))))));
    }
}
