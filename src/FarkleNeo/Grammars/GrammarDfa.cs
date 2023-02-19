// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Farkle.Grammars;

[StructLayout(LayoutKind.Auto)]
internal readonly struct GrammarDfa
{
    public readonly int StateCount, EdgeCount;

    public readonly byte EdgeIndexSize, StateIndexSize, TokenSymbolIndexSize;

    public readonly bool HasDefaultTransitions;

    public readonly int FirstEdgeBase, RangeFromBase, RangeToBase, EdgeTargetBase, AcceptBase, DefaultTransitionBase;

    private GrammarDfa(ReadOnlySpan<byte> grammarFile, int elementSize, int dfaOffset, int dfaLength, int dfaDefaultTransitionsOffset, int dfaDefaultTransitionsLength, int tokenSymbolCount) : this()
    {
        if (dfaLength < sizeof(uint) * 2)
        {
            ThrowInvalidDfaDataSize();
        }

        StateCount = (int)grammarFile.ReadUInt32(dfaOffset);
        EdgeCount = (int)grammarFile.ReadUInt32(dfaOffset + sizeof(uint));
        EdgeIndexSize = GrammarTables.GetIndexSize(EdgeCount);
        StateIndexSize = GrammarTables.GetIndexSize(StateCount);
        TokenSymbolIndexSize = GrammarTables.GetIndexSize(tokenSymbolCount);

        int expectedSize =
            sizeof(uint) * 2
            + StateCount * EdgeIndexSize
            + EdgeCount * elementSize * 2 +
            +EdgeCount * StateIndexSize
            + StateCount * TokenSymbolIndexSize;

        if (dfaLength != expectedSize)
        {
            ThrowInvalidDfaDataSize();
        }

        FirstEdgeBase = dfaOffset + sizeof(uint) * 2;
        RangeFromBase = FirstEdgeBase + StateCount * EdgeIndexSize;
        RangeToBase = RangeFromBase + EdgeCount * elementSize;
        EdgeTargetBase = RangeToBase + EdgeCount * elementSize;
        AcceptBase = EdgeTargetBase + EdgeCount * StateIndexSize;

        if (dfaDefaultTransitionsLength > 0)
        {
            if (dfaDefaultTransitionsLength != StateCount * StateIndexSize)
            {
                ThrowInvalidDfaDataSize();
            }

            HasDefaultTransitions = true;
            DefaultTransitionBase = dfaDefaultTransitionsOffset;
        }
    }

    public static unsafe GrammarDfa Create<TChar>(ReadOnlySpan<byte> grammarFile, int dfaOffset, int dfaLength, int dfaDefaultTransitionsOffset, int dfaDefaultTransitionsLength, int tokenSymbolCount)
        where TChar : unmanaged
    {
        return new(grammarFile, sizeof(TChar), dfaOffset, dfaLength, dfaDefaultTransitionsOffset, dfaDefaultTransitionsLength, tokenSymbolCount);
    }

    private int ReadFirstEdge(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)grammarFile.ReadUIntVariableSize(FirstEdgeBase + state * EdgeIndexSize, EdgeIndexSize);

    private int ReadState(ReadOnlySpan<byte> grammarFile, int @base) =>
        (int)grammarFile.ReadUIntVariableSize(@base, StateIndexSize);

    /// <summary>
    /// Searches for the next DFA state.
    /// </summary>
    /// <param name="grammarFile">The grammar's data.</param>
    /// <param name="state">The current state.</param>
    /// <param name="c">The character to advance with.</param>
    /// <returns>The index of the next state, or -1 if such state does not exist.</returns>
    public int NextState(ReadOnlySpan<byte> grammarFile, int state, char c)
    {
        if ((uint)state < (uint)StateCount)
        {
            return -1;
        }
        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int edgeLength = (state != StateCount - 1 ? ReadFirstEdge(grammarFile, state + 1) : EdgeCount) - edgeOffset;

        if (edgeLength != 0)
        {
            int edge = BufferBinarySearch.SearchChar(grammarFile, RangeToBase + edgeOffset * sizeof(char), edgeLength, c);

            if (edge < 0)
            {
                edge = Math.Min(~edge, edgeLength - 1);
            }

            char cFrom = (char)grammarFile.ReadUInt16(RangeFromBase + (edgeOffset + edge) * sizeof(char));
            char cTo = (char)grammarFile.ReadUInt16(RangeToBase + (edgeOffset + edge) * sizeof(char));

            if (cFrom <= c && c <= cTo)
            {
                return ReadState(grammarFile, EdgeTargetBase + (edgeOffset + edge) * StateIndexSize);
            }
        }

        if (HasDefaultTransitions)
        {
            return grammarFile.ReadUInt16(DefaultTransitionBase + state * StateIndexSize);
        }

        return 0;
    }

    public TokenSymbolHandle GetAcceptSymbol(ReadOnlySpan<byte> grammarFile, int state)
    {
        Debug.Assert((uint)state < (uint)StateCount);
        return new(grammarFile.ReadUIntVariableSize(AcceptBase + state * TokenSymbolIndexSize, TokenSymbolIndexSize));
    }

    [DoesNotReturn, StackTraceHidden]
    private static void ThrowInvalidDfaDataSize() =>
        ThrowHelpers.ThrowInvalidDataException("Invalid DFA data size.");
}
