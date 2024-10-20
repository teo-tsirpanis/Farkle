// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Runtime.CompilerServices;
using static Farkle.Grammars.GrammarUtilities;

namespace Farkle.Grammars.StateMachines;

internal static class StateMachineUtilities
{
    public const int AsciiCharacterCount = 128;

    public static readonly int[] DfaStateAllErrors = CreateAllErrorsState();

    private static int[] CreateAllErrorsState()
    {
        int[] array = new int[AsciiCharacterCount];
        array.AsSpan().Fill(-1);
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ReadOnlySpan<byte> buffer, int index) where T : unmanaged
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
        int low = 0, high = length;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            T midItem = Read<T>(buffer, @base + mid * sizeof(T));

            switch (midItem.CompareTo(item))
            {
                case 0:
                    return mid;
                case < 0:
                    low = mid + 1;
                    break;
                case > 0:
                    high = mid - 1;
                    break;
            }
        }

        return ~low;
    }

    private static DfaWithoutConflicts<TChar>? CreateDfa<TChar>(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions) where TChar : unmanaged, IComparable<TChar>
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

        return new DfaWithoutConflicts<TChar>(grammar, stateCount, edgeCount, grammar.GrammarTables.TokenSymbolRowCount, dfa, dfaDefaultTransitions);
    }

    private static DfaWithConflicts<TChar>? CreateDfaWithConflicts<TChar>(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions) where TChar : unmanaged, IComparable<TChar>
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

        return new DfaWithConflicts<TChar>(grammar, stateCount, edgeCount, acceptCount, grammar.GrammarTables.TokenSymbolRowCount, dfa, dfaDefaultTransitions);
    }

    private static LrWithoutConflicts? CreateLr(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection lr)
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

        return new LrWithoutConflicts(grammar, stateCount, actionCount, gotoCount, in grammar.GrammarTables, lr);
    }

    private static LrWithConflicts? CreateLrWithConflicts(Grammar grammar, ReadOnlySpan<byte> grammarFile, GrammarFileSection lr)
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

        return new LrWithConflicts(grammar, stateCount, actionCount, gotoCount, eofActionCount, in grammar.GrammarTables, lr);
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
