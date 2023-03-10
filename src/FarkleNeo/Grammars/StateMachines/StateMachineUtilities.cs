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

    public static T Read<T>(ReadOnlySpan<byte> buffer, int index)
    {
        if (typeof(T) == typeof(char))
        {
            return (T)(object)(char)buffer.ReadUInt16(index);
        }
        if (typeof(T) == typeof(byte))
        {
            return (T)(object)buffer[index];
        }
        if (typeof(T) == typeof(ushort))
        {
            return (T)(object)buffer.ReadUInt16(index);
        }
        if (typeof(T) == typeof(uint))
        {
            return (T)(object)buffer.ReadUInt32(index);
        }

        throw new NotSupportedException();
    }

    public static void WriteChar<TChar>(this IBufferWriter<byte> writer, TChar c) where TChar : unmanaged
    {
        if (typeof(TChar) == typeof(char))
        {
            writer.Write((char)(object)c);
            return;
        }

        throw new NotSupportedException();
    }

    public static T CastUInt<T>(uint value) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            return (T)(object)(byte)value;
        }
        if (typeof(T) == typeof(ushort))
        {
            return (T)(object)(ushort)value;
        }
        if (typeof(T) == typeof(uint))
        {
            return (T)(object)value;
        }

        throw new NotSupportedException();
    }

    public static unsafe int BufferBinarySearch<T>(ReadOnlySpan<byte> buffer, int @base, int length, T item) where T : unmanaged, IComparable<T>
    {
        int low = @base, high = @base + length * sizeof(T);

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            T midItem = Read<T>(buffer, mid);

            switch (midItem.CompareTo(item))
            {
                case 0:
                    return mid;
                case -1:
                    low = mid + sizeof(T);
                    break;
                case 1:
                    high = mid - sizeof(T);
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

    private static LrStateMachine? CreateLr(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection lr)
    {
        if (lr.Length < sizeof(uint) * 3)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int stateCount = (int)grammarFile.ReadUInt32(lr.Offset);
        int actionCount = (int)grammarFile.ReadUInt32(lr.Offset + sizeof(uint));
        int gotoCount = (int)grammarFile.ReadUInt32(lr.Offset + sizeof(uint) * 2);

        if (stateCount == 0)
        {
            return null;
        }

        return GetIndexSize(stateCount) switch
        {
            1 => Stage1<byte>(),
            2 => Stage1<ushort>(),
            _ => Stage1<uint>()
        };

        LrStateMachine Stage1<TStateIndex>() => GetIndexSize(actionCount) switch
        {
            1 => Stage2<TStateIndex, byte>(),
            2 => Stage2<TStateIndex, ushort>(),
            _ => Stage2<TStateIndex, uint>()
        };

        LrStateMachine Stage2<TStateIndex, TActionIndex>() => GetIndexSize(gotoCount) switch
        {
            1 => Stage3<TStateIndex, TActionIndex, byte>(),
            2 => Stage3<TStateIndex, TActionIndex, ushort>(),
            _ => Stage3<TStateIndex, TActionIndex, uint>()
        };

        LrStateMachine Stage3<TStateIndex, TActionIndex, TGotoIndex>() => LrTerminalAction.GetEncodedSize(stateCount, grammar.GrammarTables.ProductionRowCount) switch
        {
            1 => Stage4<TStateIndex, TActionIndex, TGotoIndex, sbyte>(),
            2 => Stage4<TStateIndex, TActionIndex, TGotoIndex, short>(),
            _ => Stage4<TStateIndex, TActionIndex, TGotoIndex, int>()
        };

        LrStateMachine Stage4<TStateIndex, TActionIndex, TGotoIndex, TAction>() => LrEndOfFileAction.GetEncodedSize(grammar.GrammarTables.ProductionRowCount) switch
        {
            1 => Stage5<TStateIndex, TActionIndex, TGotoIndex, TAction, byte>(),
            2 => Stage5<TStateIndex, TActionIndex, TGotoIndex, TAction, ushort>(),
            _ => Stage5<TStateIndex, TActionIndex, TGotoIndex, TAction, uint>()
        };

        LrStateMachine Stage5<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction>() => GrammarTables.GetIndexSize(grammar.GrammarTables.TokenSymbolRowCount) switch
        {
            1 => Stage6<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, byte>(),
            2 => Stage6<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, ushort>(),
            _ => Stage6<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, uint>()
        };

        LrStateMachine Stage6<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol>()
            where TTokenSymbol : unmanaged, IComparable<TTokenSymbol> => GrammarTables.GetIndexSize(grammar.GrammarTables.NonterminalRowCount) switch
            {
                1 => Finish<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, byte>(),
                2 => Finish<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, ushort>(),
                _ => Finish<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, uint>()
            };

        LrStateMachine Finish<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>()
            where TTokenSymbol : unmanaged, IComparable<TTokenSymbol>
            where TNonterminal : unmanaged, IComparable<TNonterminal> =>
            LrWithoutConflicts<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>.Create(grammar, stateCount, actionCount, gotoCount, lr);
    }

    private static LrStateMachine? CreateLrWithConflicts(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection lr)
    {
        if (lr.Length < sizeof(uint) * 4)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int stateCount = (int)grammarFile.ReadUInt32(lr.Offset);
        int actionCount = (int)grammarFile.ReadUInt32(lr.Offset + sizeof(uint));
        int gotoCount = (int)grammarFile.ReadUInt32(lr.Offset + sizeof(uint) * 2);
        int eofActionCount = (int)grammarFile.ReadUInt32(lr.Offset + sizeof(uint) * 3);

        if (stateCount == 0)
        {
            return null;
        }

        return GetIndexSize(stateCount) switch
        {
            1 => Stage1<byte>(),
            2 => Stage1<ushort>(),
            _ => Stage1<uint>()
        };

        LrStateMachine Stage1<TStateIndex>() => GetIndexSize(actionCount) switch
        {
            1 => Stage2<TStateIndex, byte>(),
            2 => Stage2<TStateIndex, ushort>(),
            _ => Stage2<TStateIndex, uint>()
        };

        LrStateMachine Stage2<TStateIndex, TActionIndex>() => GetIndexSize(gotoCount) switch
        {
            1 => Stage3<TStateIndex, TActionIndex, byte>(),
            2 => Stage3<TStateIndex, TActionIndex, ushort>(),
            _ => Stage3<TStateIndex, TActionIndex, uint>()
        };

        LrStateMachine Stage3<TStateIndex, TActionIndex, TGotoIndex>() => GetIndexSize(eofActionCount) switch
        {
            1 => Stage4<TStateIndex, TActionIndex, TGotoIndex, byte>(),
            2 => Stage4<TStateIndex, TActionIndex, TGotoIndex, ushort>(),
            _ => Stage4<TStateIndex, TActionIndex, TGotoIndex, uint>()
        };

        LrStateMachine Stage4<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex>() => LrTerminalAction.GetEncodedSize(stateCount, grammar.GrammarTables.ProductionRowCount) switch
        {
            1 => Stage5<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, sbyte>(),
            2 => Stage5<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, short>(),
            _ => Stage5<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, int>()
        };

        LrStateMachine Stage5<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction>() => LrEndOfFileAction.GetEncodedSize(grammar.GrammarTables.ProductionRowCount) switch
        {
            1 => Stage6<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, byte>(),
            2 => Stage6<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, ushort>(),
            _ => Stage6<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, uint>()
        };

        LrStateMachine Stage6<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction>() => GrammarTables.GetIndexSize(grammar.GrammarTables.TokenSymbolRowCount) switch
        {
            1 => Stage7<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, byte>(),
            2 => Stage7<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, ushort>(),
            _ => Stage7<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, uint>()
        };

        LrStateMachine Stage7<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol>()
            where TTokenSymbol : unmanaged, IComparable<TTokenSymbol> => GrammarTables.GetIndexSize(grammar.GrammarTables.NonterminalRowCount) switch
            {
                1 => Finish<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, byte>(),
                2 => Finish<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, ushort>(),
                _ => Finish<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, uint>()
            };

        LrStateMachine Finish<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>()
            where TTokenSymbol : unmanaged, IComparable<TTokenSymbol>
            where TNonterminal : unmanaged, IComparable<TNonterminal> =>
            LrWithConflicts<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>.Create(grammar, stateCount, actionCount, gotoCount, eofActionCount, lr);
    }

    public static (Dfa<char>? DfaOnChar, LrStateMachine? LrStateMachine) GetGrammarStateMachines(Grammar grammar, ReadOnlySpan<byte> grammarFile, in GrammarStateMachines stateMachines)
    {
        Dfa<char>? dfaOnChar = null;

        if (!stateMachines.DfaOnChar.IsEmpty)
        {
            dfaOnChar = CreateDfa<char>(grammar, grammarFile, stateMachines.DfaOnChar, stateMachines.DfaOnCharDefaultTransitions);
        }

        if (dfaOnChar is null && !stateMachines.DfaOnCharWithConflicts.IsEmpty)
        {
            dfaOnChar = CreateDfaWithConflicts<char>(grammar, grammarFile, stateMachines.DfaOnCharWithConflicts, stateMachines.DfaOnCharDefaultTransitions);
        }

        LrStateMachine? lr = null;

        if (!stateMachines.Lr1.IsEmpty)
        {
            lr = CreateLr(grammar, grammarFile, stateMachines.Lr1);
        }

        if (lr is null && !stateMachines.Glr1.IsEmpty)
        {
            lr = CreateLrWithConflicts(grammar, grammarFile, stateMachines.Glr1);
        }

        return (dfaOnChar, lr);
    }
}
