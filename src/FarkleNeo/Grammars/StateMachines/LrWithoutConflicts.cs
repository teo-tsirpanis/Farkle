// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithoutConflicts<TIndex>(Grammar grammar, int stateCount, int actionCount, int gotoCount)
    : LrImplementationBase<TIndex>(grammar, stateCount, actionCount, gotoCount, false)
    where TIndex : unmanaged, IComparable<TIndex>
{
    private Dictionary<TokenSymbolHandle, LrAction>[]? _actionLookup;

    internal required int EofActionBase { get; init; }

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

    internal override void PrepareForParsing()
    {
        base.PrepareForParsing();
        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        var actionLookup = new Dictionary<TokenSymbolHandle, LrAction>[Count];
        for (int i = 0; i < actionLookup.Length; i++)
        {
            var dict = new Dictionary<TokenSymbolHandle, LrAction>();
            (int actionOffset, int actionCount) = GetActionBoundsUnsafe(grammarFile, i);
            for (int j = 0; j < actionCount; j++)
            {
                ((ICollection<KeyValuePair<TokenSymbolHandle, LrAction>>)dict).Add(GetActionAtUnsafe(grammarFile, actionOffset + j));
            }
            actionLookup[i] = dict;
        }
        _actionLookup = actionLookup;
    }

    internal override LrAction GetAction(int state, TokenSymbolHandle terminal) =>
        _actionLookup![state].TryGetValue(terminal, out LrAction action) ? action : LrAction.Error;

    private LrEndOfFileAction GetEndOfFileActionUnsafe(ReadOnlySpan<byte> grammarFile, int state) =>
        new(ReadIndex(grammarFile, EofActionBase, state));

    internal override LrEndOfFileAction GetEndOfFileAction(int state)
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
