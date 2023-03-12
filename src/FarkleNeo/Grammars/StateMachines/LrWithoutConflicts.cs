// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithoutConflicts<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>
    : LrImplementationBase<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>
    where TTokenSymbol : unmanaged, IComparable<TTokenSymbol>
    where TNonterminal : unmanaged, IComparable<TNonterminal>
{
    internal required int EofActionBase { get; init; }

    public LrWithoutConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount) : base(grammar, stateCount, actionCount, gotoCount, false) { }

    public static LrWithoutConflicts<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal> Create(Grammar grammar, int stateCount, int actionCount, int gotoCount, GrammarFileSection lr)
    {
        int expectedSize =
            sizeof(uint) * 3
            + stateCount * sizeof(TActionIndex)
            + actionCount * sizeof(TTokenSymbol)
            + actionCount * sizeof(TAction)
            + stateCount * sizeof(TEofAction)
            + stateCount * sizeof(TGotoIndex)
            + gotoCount * sizeof(TNonterminal)
            + gotoCount * sizeof(TStateIndex);

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 3;
        int actionTerminalBase = firstActionBase + stateCount * sizeof(TActionIndex);
        int actionBase = actionTerminalBase + actionCount * sizeof(TTokenSymbol);
        int eofActionBase = actionBase + actionCount * sizeof(TAction);
        int firstGotoBase = eofActionBase + stateCount * sizeof(TEofAction);
        int gotoNonterminalBase = firstGotoBase + stateCount * sizeof(TGotoIndex);
        int gotoStateBase = gotoNonterminalBase + gotoCount * sizeof(TNonterminal);

        return new(grammar, stateCount, actionCount, gotoCount)
        {
            FirstActionBase = firstActionBase,
            ActionTerminalBase = actionTerminalBase,
            ActionBase = actionBase,
            EofActionBase = eofActionBase,
            FirstGotoBase = firstGotoBase,
            GotoNonterminalBase = gotoNonterminalBase,
            GotoStateBase = gotoStateBase,
        };
    }

    internal override bool StateHasConflicts(int state) => false;

    public override LrAction GetAction(int state, TokenSymbolHandle terminal)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        int actionOffset = ReadFirstAction(grammarFile, state);
        int nextActionOffset = state != Count - 1 ? ReadFirstAction(grammarFile, state + 1) : ActionCount;
        int actionCount = nextActionOffset - actionOffset;

        if (actionCount != 0)
        {
            int nextAction = StateMachineUtilities.BufferBinarySearch(grammarFile, ActionTerminalBase + actionOffset * sizeof(TTokenSymbol), actionCount, StateMachineUtilities.CastUInt<TTokenSymbol>(terminal.TableIndex));

            if (nextAction >= 0)
            {
                return ReadAction(grammarFile, actionOffset + nextAction);
            }
        }

        return LrAction.Error;
    }

    internal override LrEndOfFileAction GetEndOfFileActionAt(int index)
    {
        ValidateStateIndex(index);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;
        return new(ReadUIntVariableSizeFromArray<TActionIndex>(grammarFile, EofActionBase, index));
    }

    internal override (int Offset, int Count) GetEndOfFileActionBounds(int state)
    {
        ValidateStateIndex(state);

        if (!GetEndOfFileActionAt(state).IsError)
        {
            return (state, 1);
        }

        return (0, 0);
    }
}
