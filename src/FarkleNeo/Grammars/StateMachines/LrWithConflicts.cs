// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithConflicts<TIndex> : LrImplementationBase<TIndex>
    where TIndex : unmanaged, IComparable<TIndex>
{
    private readonly int _eofActionCount;

    internal required int FirstEofActionBase { get; init; }

    internal required int EofActionBase { get; init; }

    public LrWithConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount, int eofActionCount) : base(grammar, stateCount, actionCount, gotoCount, true)
    {
        _eofActionCount = eofActionCount;
    }

    public static LrWithConflicts<TIndex> Create(Grammar grammar, int stateCount, int actionCount, int gotoCount, int eofActionCount, GrammarFileSection lr)
    {
        int expectedSize =
            sizeof(uint) * 4
            + stateCount * sizeof(TIndex)
            + actionCount * sizeof(TIndex)
            + actionCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex)
            + eofActionCount * sizeof(TIndex)
            + stateCount * sizeof(TIndex)
            + gotoCount * sizeof(TIndex)
            + gotoCount * sizeof(TIndex);

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 4;
        int actionTerminalBase = firstActionBase + stateCount * sizeof(TIndex);
        int actionBase = actionTerminalBase + actionCount * sizeof(TIndex);
        int firstEofActionBase = actionBase + actionCount * sizeof(TIndex);
        int eofActionBase = firstEofActionBase + stateCount * sizeof(TIndex);
        int firstGotoBase = eofActionBase + eofActionCount * sizeof(TIndex);
        int gotoNonterminalBase = firstGotoBase + stateCount * sizeof(TIndex);
        int gotoStateBase = gotoNonterminalBase + gotoCount * sizeof(TIndex);

        return new(grammar, stateCount, actionCount, gotoCount, eofActionCount)
        {
            FirstActionBase = firstActionBase,
            ActionTerminalBase = actionTerminalBase,
            ActionBase = actionBase,
            FirstEofActionBase = firstEofActionBase,
            EofActionBase = eofActionBase,
            FirstGotoBase = firstGotoBase,
            GotoNonterminalBase = gotoNonterminalBase,
            GotoStateBase = gotoStateBase,
        };
    }

    internal override LrAction GetAction(int state, TokenSymbolHandle terminal) =>
        throw CreateHasConflictsException();

    internal override LrEndOfFileAction GetEndOfFileAction(int state) =>
        throw CreateHasConflictsException();

    private LrEndOfFileAction GetEndOfFileActionAtUnsafe(ReadOnlySpan<byte> grammarFile, int index) =>
        new(ReadIndex(grammarFile, EofActionBase, index));

    private int ReadFirstEofAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadIndex(grammarFile, FirstEofActionBase, state);

    internal override LrEndOfFileAction GetEndOfFileActionAt(int index)
    {
        if ((uint)index >= (uint)_eofActionCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
        }
        return GetEndOfFileActionAtUnsafe(Grammar.GrammarFile, index);
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
            ValidateAction(GetEndOfFileActionAtUnsafe(grammarFile, i), in grammarTables);
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
