// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal static class StateMachineUtilities
{
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

    public static void WriteChar<TChar>(this IBufferWriter<byte> writer, TChar c) where TChar : unmanaged
    {
        if (typeof(TChar) == typeof(char))
        {
            writer.Write((char)(object)c);
        }

        ThrowUnsupportedDfaCharacter();
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

    private static Dfa<TChar>? CreateDfa<TChar>(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions) where TChar : unmanaged, IComparable<TChar>
    {
        if (dfa.Length < sizeof(uint) * 2)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int stateCount = (int)grammarFile.ReadUInt32(dfa.Offset);
        int edgeCount = (int)grammarFile.ReadUInt32(dfa.Offset + sizeof(uint));

        if (stateCount == 0)
        {
            return null;
        }

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
            DfaWithoutConflicts<TChar, TState, TEdge, TTokenSymbol>.Create(grammar, stateCount, edgeCount, dfa, dfaDefaultTransitions);
    }

    private static Dfa<TChar>? CreateDfaWithConflicts<TChar>(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions) where TChar : unmanaged, IComparable<TChar>
    {
        if (dfa.Length < sizeof(uint) * 3)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int stateCount = (int)grammarFile.ReadUInt32(dfa.Offset);
        int edgeCount = (int)grammarFile.ReadUInt32(dfa.Offset + sizeof(uint));
        int acceptCount = (int)grammarFile.ReadUInt32(dfa.Offset + sizeof(uint) * 2);

        if (stateCount == 0)
        {
            return null;
        }

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
            <= byte.MaxValue => Stage3<TState, TEdge, byte>(),
            <= ushort.MaxValue => Stage3<TState, TEdge, ushort>(),
            <= int.MaxValue => Stage3<TState, TEdge, int>()
        };

        Dfa<TChar> Stage3<TState, TEdge, TTokenSymbol>() => acceptCount switch
        {
            <= byte.MaxValue => Finish<TState, TEdge, TTokenSymbol, byte>(),
            <= ushort.MaxValue => Finish<TState, TEdge, TTokenSymbol, ushort>(),
            <= int.MaxValue => Finish<TState, TEdge, TTokenSymbol, int>()
        };

        Dfa<TChar> Finish<TState, TEdge, TTokenSymbol, TAccept>() =>
            DfaWithConflicts<TChar, TState, TEdge, TTokenSymbol, TAccept>.Create(grammar, stateCount, edgeCount, acceptCount, dfa, dfaDefaultTransitions);
    }

    public static Dfa<char>? GetGrammarStateMachines(Grammar grammar, ReadOnlySpan<byte> grammarFile, in GrammarStateMachines stateMachines)
    {
        if (!stateMachines.DfaOnChar.IsEmpty)
        {
            return CreateDfa<char>(grammar, grammarFile, stateMachines.DfaOnChar, stateMachines.DfaOnCharDefaultTransitions);
        }

        if (!stateMachines.DfaOnCharWithConflicts.IsEmpty)
        {
            return CreateDfaWithConflicts<char>(grammar, grammarFile, stateMachines.DfaOnCharWithConflicts, stateMachines.DfaOnCharDefaultTransitions);
        }

        return null;
    }
}
