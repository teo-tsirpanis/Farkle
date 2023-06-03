// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithoutConflicts<TIndex> : LrImplementationBase<TIndex>
    where TIndex : unmanaged, IComparable<TIndex>
{
    internal required int EofActionBase { get; init; }

    public LrWithoutConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount) : base(grammar, stateCount, actionCount, gotoCount, false) { }

    public static LrWithoutConflicts<TIndex> Create(Grammar grammar, int stateCount, int actionCount, int gotoCount, GrammarFileSection lr)
    {
        int expectedSize =
            sizeof(uint) * 3
            + stateCount * sizeof(TIndex)
            + actionCount * sizeof(TIndex)
            + actionCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex)
            + gotoCount * sizeof(TIndex)
            + gotoCount * sizeof(TIndex);

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 3;
        int actionTerminalBase = firstActionBase + stateCount * sizeof(TIndex);
        int actionBase = actionTerminalBase + actionCount * sizeof(TIndex);
        int eofActionBase = actionBase + actionCount * sizeof(TIndex);
        int firstGotoBase = eofActionBase + stateCount * sizeof(TIndex);
        int gotoNonterminalBase = firstGotoBase + stateCount * sizeof(TIndex);
        int gotoStateBase = gotoNonterminalBase + gotoCount * sizeof(TIndex);

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
            int searchBase = ActionTerminalBase + actionOffset * sizeof(TIndex);
            TIndex searchIndex = StateMachineUtilities.CastUInt<TIndex>(terminal.TableIndex);
            int nextAction = StateMachineUtilities.BufferBinarySearch(grammarFile, searchBase, actionCount, searchIndex);

            if (nextAction >= 0)
            {
                return ReadAction(grammarFile, actionOffset + nextAction);
            }
        }

        return LrAction.Error;
    }

    private LrEndOfFileAction GetEndOfFileActionUnsafe(ReadOnlySpan<byte> grammarFile, int state) =>
        new(ReadIndex(grammarFile, EofActionBase, state));

    public override LrEndOfFileAction GetEndOfFileAction(int state)
    {
        ValidateStateIndex(state);
        return GetEndOfFileActionUnsafe(Grammar.GrammarFile, state);
    }

    internal override LrEndOfFileAction GetEndOfFileActionAt(int index) => GetEndOfFileAction(index);

    internal override (int Offset, int Count) GetEndOfFileActionBounds(int state)
    {
        ValidateStateIndex(state);

        if (!GetEndOfFileActionUnsafe(Grammar.GrammarFile, state).IsError)
        {
            return (state, 1);
        }

        return (0, 0);
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        base.ValidateContent(grammarFile, grammarTables);

        for (int i = 0; i < Count; i++)
        {
            ValidateAction(GetEndOfFileActionUnsafe(grammarFile, i), in grammarTables);
        }
    }
}
