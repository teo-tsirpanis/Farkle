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
    protected Grammar Grammar { get; }

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

    protected LrTerminalAction ReadAction(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadIntVariableSize<TAction>(ActionBase + index * sizeof(TAction)));

    protected int ReadFirstAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray<TActionIndex>(grammarFile, FirstActionBase, state);

    protected int ReadFirstGoto(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray<TGotoIndex>(grammarFile, FirstGotoBase, state);

    protected int ReadGoto(ReadOnlySpan<byte> grammarFile, int index) =>
        (int)ReadUIntVariableSizeFromArray<TStateIndex>(grammarFile, GotoStateBase + index * sizeof(TStateIndex), 0);

    protected static uint ReadUIntVariableSizeFromArray<T>(ReadOnlySpan<byte> grammarFile, int @base, int index) =>
        grammarFile.ReadUIntVariableSize<T>(@base + index * sizeof(T));

    internal sealed override (int Offset, int Count) GetActionBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        int actionOffset = ReadFirstAction(grammarFile, state);
        int nextActionOffset = state != Count - 1 ? ReadFirstAction(grammarFile, state + 1) : ActionCount;

        return (actionOffset, nextActionOffset - actionOffset);
    }

    internal sealed override KeyValuePair<TokenSymbolHandle, LrTerminalAction> GetActionAt(int index)
    {
        if ((uint)index >= (uint)ActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        TokenSymbolHandle terminal = new(ReadUIntVariableSizeFromArray<TTokenSymbol>(grammarFile, ActionTerminalBase, index));
        LrTerminalAction action = ReadAction(grammarFile, index);

        return new(terminal, action);
    }

    internal sealed override (int Offset, int Count) GetGotoBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        int gotoOffset = ReadFirstGoto(grammarFile, state);
        int nextGotoOffset = state != Count - 1 ? ReadFirstGoto(grammarFile, state + 1) : GotoCount;

        return (gotoOffset, nextGotoOffset - gotoOffset);
    }

    internal sealed override KeyValuePair<NonterminalHandle, int> GetGotoAt(int index)
    {
        if ((uint)index >= (uint)ActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        NonterminalHandle nonterminal = new(ReadUIntVariableSizeFromArray<TNonterminal>(grammarFile, GotoNonterminalBase, index));
        int state = ReadGoto(grammarFile, index);

        return new(nonterminal, state);
    }

    protected void ValidateStateIndex(int state, [CallerArgumentExpression(nameof(state))] string? paramName = null)
    {
        if ((uint)state >= (uint)Count)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(paramName);
        }
    }
}
