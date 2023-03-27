// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class LrWithConflicts<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>
    : LrImplementationBase<TStateIndex, TActionIndex, TGotoIndex, TAction, TEofAction, TTokenSymbol, TNonterminal>
    where TTokenSymbol : unmanaged, IComparable<TTokenSymbol>
    where TNonterminal : unmanaged, IComparable<TNonterminal>
{
    private readonly int _eofActionCount;

    internal required int FirstEofActionBase { get; init; }

    internal required int EofActionBase { get; init; }

    public LrWithConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount, int eofActionCount) : base(grammar, stateCount, actionCount, gotoCount, true)
    {
        Debug.Assert(GrammarUtilities.GetCompressedIndexSize(eofActionCount) == sizeof(TEofActionIndex));

        _eofActionCount = eofActionCount;
    }

    public static LrWithConflicts<TStateIndex, TActionIndex, TGotoIndex, TEofActionIndex, TAction, TEofAction, TTokenSymbol, TNonterminal> Create(Grammar grammar, int stateCount, int actionCount, int gotoCount, int eofActionCount, GrammarFileSection lr)
    {
        int expectedSize =
            sizeof(uint) * 4
            + stateCount * sizeof(TActionIndex)
            + actionCount * sizeof(TTokenSymbol)
            + actionCount * sizeof(TAction)
            + stateCount * sizeof(TEofActionIndex)
            + eofActionCount * sizeof(TEofAction)
            + stateCount * sizeof(TGotoIndex)
            + gotoCount * sizeof(TNonterminal)
            + gotoCount * sizeof(TStateIndex);

        if (lr.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidLrDataSize();
        }

        int firstActionBase = lr.Offset + sizeof(uint) * 4;
        int actionTerminalBase = firstActionBase + stateCount * sizeof(TActionIndex);
        int actionBase = actionTerminalBase + actionCount * sizeof(TTokenSymbol);
        int firstEofActionBase = actionBase + actionCount * sizeof(TAction);
        int eofActionBase = firstEofActionBase + stateCount * sizeof(TEofActionIndex);
        int firstGotoBase = eofActionBase + eofActionCount * sizeof(TEofAction);
        int gotoNonterminalBase = firstGotoBase + stateCount * sizeof(TGotoIndex);
        int gotoStateBase = gotoNonterminalBase + gotoCount * sizeof(TNonterminal);

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

    public override LrAction GetAction(int state, TokenSymbolHandle terminal) =>
        throw CreateHasConflictsException();

    public override LrEndOfFileAction GetEndOfFileAction(int state) =>
        throw CreateHasConflictsException();

    private LrEndOfFileAction GetEndOfFileActionAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        return new(ReadUIntVariableSizeFromArray<TEofAction>(grammarFile, EofActionBase, index));
    }

    private int ReadFirstEofAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray<TEofActionIndex>(grammarFile, FirstEofActionBase, state);

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
                Assert(firstEof > previousFirstEof);
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
