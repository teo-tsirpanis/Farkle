// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars;

internal static class StateMachineUtilities
{
    [DoesNotReturn, StackTraceHidden]
    internal static void ThrowInvalidDfaDataSize() =>
        ThrowHelpers.ThrowInvalidDataException("Invalid DFA data size.");

    [DoesNotReturn]
    private static void ThrowUnsupportedDfaCharacter() =>
        throw new NotSupportedException("Unsupported DFA character type. You can add support for it by editing the DfaUtilities class.");

    public static TChar ReadChar<TChar>(ReadOnlySpan<byte> buffer, int index)
    {
        if (typeof(TChar) == typeof(char))
        {
            return (TChar)(object)(char)buffer.ReadUInt16(index);
        }

        ThrowUnsupportedDfaCharacter();
        return default;
    }

    public static unsafe int BufferBinarySearch<TChar>(ReadOnlySpan<byte> buffer, int @base, int length, TChar item) where TChar : unmanaged, IComparable<TChar>
    {
        int low = @base, high = @base + length * sizeof(TChar);

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            TChar midItem = ReadChar<TChar>(buffer, mid);

            switch (midItem.CompareTo(item))
            {
                case 0:
                    return mid;
                case -1:
                    low = mid + sizeof(TChar);
                    break;
                case 1:
                    high = mid - sizeof(TChar);
                    break;
            }
        }

        return ~low;
    }

    public static Dfa<TChar> CreateDfa<TChar>(Grammar grammar, ReadOnlySpan<byte> grammarFile, int dfaOffset, int dfaLength, int dfaDefaultTransitionsOffset, int dfaDefaultTransitionsLength) where TChar : unmanaged, IComparable<TChar>
    {
        if (dfaLength < sizeof(uint) * 2)
        {
            ThrowInvalidDfaDataSize();
        }

        int stateCount = (int)grammarFile.ReadUInt32(dfaOffset);
        int edgeCount = (int)grammarFile.ReadUInt32(dfaOffset + sizeof(uint));

        return stateCount switch
        {
            <= byte.MaxValue => Stage1<byte>(),
            <= ushort.MaxValue => Stage1<ushort>(),
            <= int.MaxValue => Stage1<int>()
        };

        Dfa<TChar> Stage1<TState>() => edgeCount switch
        {
            <= byte.MaxValue => Stage2<TState, byte>(),
            <= ushort.MaxValue => Stage2<TState, ushort>(),
            <= int.MaxValue => Stage2<TState, int>()
        };

        Dfa<TChar> Stage2<TState, TEdge>() => grammar.GrammarTables.TokenSymbolRowCount switch
        {
            <= byte.MaxValue => Finish<TState, TEdge, byte>(),
            <= ushort.MaxValue => Finish<TState, TEdge, ushort>(),
            <= int.MaxValue => Finish<TState, TEdge, int>()
        };

        Dfa<TChar> Finish<TState, TEdge, TTokenSymbol>() =>
            new GrammarDfa<TChar, TState, TEdge, TTokenSymbol>(grammar, stateCount, edgeCount, dfaOffset, dfaLength, dfaDefaultTransitionsOffset, dfaDefaultTransitionsLength);
    }
}
