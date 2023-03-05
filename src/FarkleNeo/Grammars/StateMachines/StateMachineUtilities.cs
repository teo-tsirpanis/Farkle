// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal static class StateMachineUtilities
{
    /// <summary>
    /// Gets the size in bytes of a zero-based index to a state machine construct.
    /// </summary>
    public static byte GetIndexSize(int count) => count switch
    {
        // If we have say 256 states, they fit in a byte; the first has an index of 0, the last has an index of 255.
        <= byte.MaxValue + 1 => sizeof(byte),
        <= ushort.MaxValue + 1 => sizeof(ushort),
        _ => sizeof(uint)
    };

    /// <summary>
    /// Gets the size in bytes of an index to a DFA target state.
    /// </summary>
    internal static byte GetDfaStateTargetIndexSize(int stateCount) => stateCount switch
    {
        // If we have say 255 states, they fit in a byte; the first has an index of 1,
        // the last has an index of 255 and failure is represented as 0.
        <= byte.MaxValue => sizeof(byte),
        <= ushort.MaxValue => sizeof(ushort),
        _ => sizeof(uint)
    };

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

        return GetDfaStateTargetIndexSize(stateCount) switch
        {
            1 => Stage1<byte>(),
            2 => Stage1<ushort>(),
            _ => Stage1<uint>()
        };

        Dfa<TChar> Stage1<TState>() => GetIndexSize(edgeCount) switch
        {
            1 => Stage2<TState, byte>(),
            2 => Stage2<TState, ushort>(),
            _ => Stage2<TState, uint>()
        };

        Dfa<TChar> Stage2<TState, TEdge>() => GrammarTables.GetIndexSize(grammar.GrammarTables.TokenSymbolRowCount) switch
        {
            1 => Finish<TState, TEdge, byte>(),
            2 => Finish<TState, TEdge, ushort>(),
            _ => Finish<TState, TEdge, uint>()
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

        return GetDfaStateTargetIndexSize(stateCount) switch
        {
            1 => Stage1<byte>(),
            2 => Stage1<ushort>(),
            _ => Stage1<uint>()
        };

        Dfa<TChar> Stage1<TState>() => GetIndexSize(edgeCount) switch
        {
            1 => Stage2<TState, byte>(),
            2 => Stage2<TState, ushort>(),
            _ => Stage2<TState, uint>()
        };

        Dfa<TChar> Stage2<TState, TEdge>() => GrammarTables.GetIndexSize(grammar.GrammarTables.TokenSymbolRowCount) switch
        {
            1 => Stage3<TState, TEdge, byte>(),
            2 => Stage3<TState, TEdge, ushort>(),
            _ => Stage3<TState, TEdge, uint>()
        };

        Dfa<TChar> Stage3<TState, TEdge, TTokenSymbol>() => GetIndexSize(acceptCount) switch
        {
            1 => Finish<TState, TEdge, TTokenSymbol, byte>(),
            2 => Finish<TState, TEdge, TTokenSymbol, ushort>(),
            _ => Finish<TState, TEdge, TTokenSymbol, uint>()
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
