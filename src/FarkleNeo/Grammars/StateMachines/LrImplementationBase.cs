// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe abstract class LrImplementationBase<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal> : LrStateMachine
    where TTokenSymbol : unmanaged, IComparable<TTokenSymbol>
    where TNonterminal : unmanaged, IComparable<TNonterminal>
{
    protected int ActionCount { get; }

    protected int GotoCount { get; }

    public required int FirstActionBase { get; init; }

    public required int ActionTerminalBase { get; init; }

    public required int ActionBase { get; init; }

    public required int FirstGotoBase { get; init; }

    public required int GotoNonterminalBase { get; init; }

    public required int GotoStateBase { get; init; }

    protected unsafe LrImplementationBase(Grammar grammar, int stateCount, int actionCount, int gotoCount, bool hasConflicts) : base(stateCount, hasConflicts)
    {
        Debug.Assert(StateMachineUtilities.GetIndexSize(stateCount) == sizeof(TStateIndex));
        Debug.Assert(StateMachineUtilities.GetIndexSize(actionCount) == sizeof(TActionIndex));
        Debug.Assert(StateMachineUtilities.GetIndexSize(gotoCount) == sizeof(TGotoIndex));

        Grammar = grammar;
        ActionCount = actionCount;
        GotoCount = gotoCount;
    }

    protected LrAction ReadAction(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadIntVariableSize<TAction>(ActionBase + index * sizeof(TAction)));

    protected int ReadFirstAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray<TActionIndex>(grammarFile, FirstActionBase, state);

    protected int ReadFirstGoto(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray<TGotoIndex>(grammarFile, FirstGotoBase, state);

    protected int ReadGoto(ReadOnlySpan<byte> grammarFile, int index) =>
        (int)ReadUIntVariableSizeFromArray<TStateIndex>(grammarFile, GotoStateBase + index * sizeof(TStateIndex), 0);

    protected static uint ReadUIntVariableSizeFromArray<T>(ReadOnlySpan<byte> grammarFile, int @base, int index) =>
        grammarFile.ReadUIntVariableSize<T>(@base + index * sizeof(T));

    internal sealed override Grammar Grammar { get; }

    public sealed override int GetGoto(int state, NonterminalHandle nonterminal)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        int gotoOffset = ReadFirstGoto(grammarFile, state);
        int nextGotoOffset = state != Count - 1 ? ReadFirstGoto(grammarFile, state + 1) : GotoCount;
        int gotoCount = nextGotoOffset - gotoOffset;

        if (gotoCount != 0)
        {
            int nextGoto = StateMachineUtilities.BufferBinarySearch(grammarFile, GotoNonterminalBase + gotoOffset * sizeof(TNonterminal), gotoCount, StateMachineUtilities.CastUInt<TNonterminal>(nonterminal.TableIndex));

            if (nextGoto >= 0)
            {
                return ReadGoto(grammarFile, gotoOffset + nextGoto);
            }
        }

        ThrowHelpers.ThrowKeyNotFoundException("Could not find GOTO transition");
        return 0;
    }

    private (int Offset, int Count) GetActionBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int actionOffset = ReadFirstAction(grammarFile, state);
        int nextActionOffset = state != Count - 1 ? ReadFirstAction(grammarFile, state + 1) : ActionCount;
        return (actionOffset, nextActionOffset - actionOffset);
    }

    private KeyValuePair<TokenSymbolHandle, LrAction> GetActionAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        TokenSymbolHandle terminal = new(ReadUIntVariableSizeFromArray<TTokenSymbol>(grammarFile, ActionTerminalBase, index));
        LrAction action = ReadAction(grammarFile, index);
        return new(terminal, action);
    }

    private (int Offset, int Count) GetGotoBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int gotoOffset = ReadFirstGoto(grammarFile, state);
        int nextGotoOffset = state != Count - 1 ? ReadFirstGoto(grammarFile, state + 1) : GotoCount;
        return (gotoOffset, nextGotoOffset - gotoOffset);
    }

    private KeyValuePair<NonterminalHandle, int> GetGotoAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        NonterminalHandle nonterminal = new(ReadUIntVariableSizeFromArray<TNonterminal>(grammarFile, GotoNonterminalBase, index));
        int state = ReadGoto(grammarFile, index);
        return new(nonterminal, state);
    }

    internal sealed override (int Offset, int Count) GetActionBounds(int state)
    {
        ValidateStateIndex(state);
        return GetActionBoundsUnsafe(Grammar.GrammarFile, state);
    }

    internal sealed override KeyValuePair<TokenSymbolHandle, LrAction> GetActionAt(int index)
    {
        if ((uint)index >= (uint)ActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return GetActionAtUnsafe(Grammar.GrammarFile, index);
    }

    internal sealed override (int Offset, int Count) GetGotoBounds(int state)
    {
        ValidateStateIndex(state);
        return GetGotoBoundsUnsafe(Grammar.GrammarFile, state);
    }

    internal sealed override KeyValuePair<NonterminalHandle, int> GetGotoAt(int index)
    {
        if ((uint)index >= (uint)ActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return GetGotoAtUnsafe(Grammar.GrammarFile, index);
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        {
            int previousFirstAction = ReadFirstAction(grammarFile, 0);
            int previousFirstGoto = ReadFirstGoto(grammarFile, 0);
            Assert(previousFirstAction == 0);
            Assert(previousFirstGoto == 0);
            for (int i = 1; i < Count; i++)
            {
                int firstAction = ReadFirstAction(grammarFile, i);
                Assert(firstAction >= previousFirstAction, "LR state first action is out of sequence.");
                previousFirstAction = firstAction;
                // We don't have to do an unsigned check; ActionCount has been read from an unsigned integer.
                Assert(firstAction <= ActionCount);

                int firstGoto = ReadFirstGoto(grammarFile, i);
                Assert(firstGoto >= previousFirstGoto, "LR state first goto is out of sequence.");
                previousFirstGoto = firstGoto;
                // We don't have to do an unsigned check; GotoCount has been read from an unsigned integer.
                Assert(firstGoto <= GotoCount);
            }
        }

        // State machines with conflicts support duplicate actions.
        // With this we determine once if we want them or not.
        int actionComparisonKey = HasConflicts ? 0 : -1;
        for (int i = 0; i < Count; i++)
        {
            (int actionOffset, int actionCount) = GetActionBoundsUnsafe(grammarFile, i);
            if (actionCount > 0)
            {
                uint previousActionTerminal = GetActionAtUnsafe(grammarFile, actionOffset).Key.TableIndex;
                for (int j = 0; j < actionCount; j++)
                {
                    KeyValuePair<TokenSymbolHandle, LrAction> action = GetActionAtUnsafe(grammarFile, actionOffset + j);
                    grammarTables.ValidateHandle(action.Key);
                    Assert(grammarTables.IsTerminal(action.Key));
                    if (j != 0)
                    {
                        Assert(previousActionTerminal.CompareTo(action.Key.TableIndex) <= actionComparisonKey, "LR state terminal is out of sequence or unexpected LR conflict.");
                    }
                    previousActionTerminal = action.Key.TableIndex;
                    ValidateAction(action.Value, in grammarTables);
                }
            }

            (int gotoOffset, int gotoCount) = GetGotoBoundsUnsafe(grammarFile, i);
            if (GotoCount > 0)
            {
                uint previousGotoNonterminal = GetGotoAtUnsafe(grammarFile, gotoOffset).Key.TableIndex;
                for (int j = 0; j < gotoCount; j++)
                {
                    KeyValuePair<NonterminalHandle, int> @goto = GetGotoAtUnsafe(grammarFile, gotoOffset + j);
                    grammarTables.ValidateHandle(@goto.Key);
                    if (j != 0)
                    {
                        switch (previousGotoNonterminal.CompareTo(@goto.Key.TableIndex))
                        {
                            case 0:
                                ThrowHelpers.ThrowInvalidDataException("GOTO conflicts are not allowed.");
                                break;
                            case > 0:
                                ThrowHelpers.ThrowInvalidDataException("GOTO nonterminal is out of sequence.");
                                break;
                        }
                    }
                    previousGotoNonterminal = @goto.Key.TableIndex;
                    ValidateStateIndex(@goto.Value);
                }
            }
        }

        static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                ThrowHelpers.ThrowInvalidDataException(message);
            }
        }
    }

    private void ValidateAction(LrAction action, in GrammarTables grammarTables)
    {
        if (action.IsReduce)
        {
            grammarTables.ValidateHandle(action.ReduceProduction);
        }
        else if (action.IsShift)
        {
            ValidateStateIndex(action.ShiftState);
        }
    }

    protected static void ValidateAction(LrEndOfFileAction action, in GrammarTables grammarTables)
    {
        if (action.IsReduce)
        {
            grammarTables.ValidateHandle(action.ReduceProduction);
        }
    }

    protected void ValidateStateIndex(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= (uint)Count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }
}
