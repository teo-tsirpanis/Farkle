// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithoutConflicts : LrImplementationBase
{
    private Dictionary<TokenSymbolHandle, LrAction>[]? _actionLookup;

    [SetsRequiredMembers]
    public LrWithoutConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount, in GrammarTables grammarTables, GrammarFileSection lr)
        : base(grammar, stateCount, actionCount, gotoCount, in grammarTables, false)
    {
        int expectedSize =
            sizeof(uint) * 3
            + stateCount * _actionIndexSize
            + actionCount * _tokenSymbolIndexSize
            + actionCount * _actionSize
            + stateCount * _eofActionSize
            + stateCount * _gotoIndexSize
            + gotoCount * _nonterminalIndexSize
            + gotoCount * _stateIndexSize;

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 3;
        int actionTerminalBase = firstActionBase + stateCount * _actionIndexSize;
        int actionBase = actionTerminalBase + actionCount * _tokenSymbolIndexSize;
        int eofActionBase = actionBase + actionCount * _actionSize;
        int firstGotoBase = eofActionBase + stateCount * _eofActionSize;
        int gotoNonterminalBase = firstGotoBase + stateCount * _gotoIndexSize;
        int gotoStateBase = gotoNonterminalBase + gotoCount * _nonterminalIndexSize;

        FirstActionBase = firstActionBase;
        ActionTerminalBase = actionTerminalBase;
        ActionBase = actionBase;
        EofActionBase = eofActionBase;
        FirstGotoBase = firstGotoBase;
        GotoNonterminalBase = gotoNonterminalBase;
        GotoStateBase = gotoStateBase;
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

    internal override LrEndOfFileAction GetEndOfFileAction(int state)
    {
        ValidateStateIndex(state);
        return ReadEofAction(Grammar.GrammarFile, state);
    }

    internal override LrEndOfFileAction GetEndOfFileActionAt(int index) => GetEndOfFileAction(index);

    internal override (int Offset, int Count) GetEndOfFileActionBounds(int state)
    {
        ValidateStateIndex(state);

        if (!ReadEofAction(Grammar.GrammarFile, state).IsError)
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
            ValidateAction(ReadEofAction(grammarFile, i), in grammarTables);
        }
    }
}
