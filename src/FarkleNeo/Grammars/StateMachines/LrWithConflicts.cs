// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithConflicts : LrImplementationBase
{
    private readonly byte _eofActionIndexSize;

    private readonly int _eofActionCount;

    internal required int FirstEofActionBase { get; init; }

    [SetsRequiredMembers]
    public LrWithConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount, int eofActionCount, in GrammarTables grammarTables, GrammarFileSection lr)
        : base(grammar, stateCount, actionCount, gotoCount, in grammarTables, true)
    {
        _eofActionIndexSize = GrammarUtilities.GetCompressedIndexSize(eofActionCount);
        _eofActionCount = eofActionCount;

        int expectedSize =
            sizeof(uint) * 4
            + stateCount * _actionIndexSize
            + actionCount * _tokenSymbolIndexSize
            + actionCount * _actionSize
            + stateCount * _eofActionIndexSize
            + eofActionCount * _eofActionSize
            + stateCount * _gotoIndexSize
            + gotoCount * _nonterminalIndexSize
            + gotoCount * _stateIndexSize;

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 4;
        int actionTerminalBase = firstActionBase + stateCount * _actionIndexSize;
        int actionBase = actionTerminalBase + actionCount * _tokenSymbolIndexSize;
        int firstEofActionBase = actionBase + actionCount * _actionSize;
        int eofActionBase = firstEofActionBase + stateCount * _eofActionIndexSize;
        int firstGotoBase = eofActionBase + eofActionCount * _eofActionSize;
        int gotoNonterminalBase = firstGotoBase + stateCount * _gotoIndexSize;
        int gotoStateBase = gotoNonterminalBase + gotoCount * _nonterminalIndexSize;

        FirstActionBase = firstActionBase;
        ActionTerminalBase = actionTerminalBase;
        ActionBase = actionBase;
        FirstEofActionBase = firstEofActionBase;
        EofActionBase = eofActionBase;
        FirstGotoBase = firstGotoBase;
        GotoNonterminalBase = gotoNonterminalBase;
        GotoStateBase = gotoStateBase;
    }

    internal override LrAction GetAction(int state, TokenSymbolHandle terminal) =>
        throw CreateHasConflictsException();

    internal override LrEndOfFileAction GetEndOfFileAction(int state) =>
        throw CreateHasConflictsException();

    private int ReadFirstEofAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray(grammarFile, FirstEofActionBase, state, _eofActionIndexSize);

    internal override LrEndOfFileAction GetEndOfFileActionAt(int index)
    {
        if ((uint)index >= (uint)_eofActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return ReadEofAction(Grammar.GrammarFile, index);
    }

    internal override (int Offset, int Count) GetEndOfFileActionBounds(int state)
    {
        ValidateStateIndex(state);

        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        int firstEofAction = ReadFirstEofAction(grammarFile, state);
        int nextFirstEofAction = state != Count - 1 ? ReadFirstEofAction(grammarFile, state + 1) : _eofActionCount;

        return (firstEofAction, nextFirstEofAction - firstEofAction);
    }

    private static NotSupportedException CreateHasConflictsException() =>
        new("State machine has conflicts.");

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        base.ValidateContent(grammarFile, grammarTables);

        if (_eofActionCount > 0)
        {
            int previousFirstEof = ReadFirstEofAction(grammarFile, 0);
            Assert(previousFirstEof == 0);
            for (int i = 1; i < Count; i++)
            {
                int firstEof = ReadFirstEofAction(grammarFile, i);
                Assert(firstEof >= previousFirstEof, "LR state first EOF action is out of sequence");
                previousFirstEof = firstEof;
                Assert(firstEof <= _eofActionCount);
            }
        }

        for (int i = 0; i < _eofActionCount; i++)
        {
            ValidateAction(ReadEofAction(grammarFile, i), in grammarTables);
        }

        static void Assert([DoesNotReturnIf(false)] bool condition, string? message = null)
        {
            if (!condition)
            {
                ThrowHelpers.ThrowInvalidDataException(message);
            }
        }
    }
}
