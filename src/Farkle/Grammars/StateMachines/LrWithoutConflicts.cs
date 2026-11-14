// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars.StateMachines;

internal sealed partial class LrWithoutConflicts : LrImplementationBase
{
    [SetsRequiredMembers]
    public LrWithoutConflicts(Grammar grammar, int stateCount, int actionCount, int gotoCount, in GrammarTables grammarTables, GrammarFileSection lr, GrammarFileSection lrDefaultReductions)
        : base(grammar, stateCount, actionCount, gotoCount, in grammarTables, lrDefaultReductions, false)
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

        FirstActionBase = lr.Offset + sizeof(uint) * 3;
        ActionTerminalBase = FirstActionBase + stateCount * _actionIndexSize;
        ActionBase = ActionTerminalBase + actionCount * _tokenSymbolIndexSize;
        EofActionBase = ActionBase + actionCount * _actionSize;
        FirstGotoBase = EofActionBase + stateCount * _eofActionSize;
        GotoNonterminalBase = FirstGotoBase + stateCount * _gotoIndexSize;
        GotoStateBase = GotoNonterminalBase + gotoCount * _nonterminalIndexSize;
    }

    internal override bool StateHasConflicts(int state) => false;

    /// <summary>
    /// Returns the number of empty terminal slots (gaps) between the first and the last action for the given state.
    /// </summary>
    private int GetActionGapCountUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        var (offset, count) = GetActionBoundsUnsafe(grammarFile, state);
        if (count == 0)
        {
            return 0;
        }
        uint previousIdx = ReadActionTerminal(grammarFile, offset).TableIndex;
        int gaps = 0;
        for (int i = offset + 1; i < offset + count; i++)
        {
            uint idx = ReadActionTerminal(grammarFile, i).TableIndex;
            gaps += (int)(idx - previousIdx - 1);
            previousIdx = idx;
        }
        return gaps;
    }

    /// <summary>
    /// Returns whether the two given states have the same <see cref="LrState.Actions"/>.
    /// </summary>
    private bool AreActionsEqualUnsafe(ReadOnlySpan<byte> grammarFile, int state1, int state2)
    {
        if (state1 == state2)
        {
            return true;
        }
        var (offset1, count1) = GetActionBoundsUnsafe(grammarFile, state1);
        var (offset2, count2) = GetActionBoundsUnsafe(grammarFile, state2);
        if (count1 != count2)
        {
            return false;
        }
        var span1 = grammarFile.Slice(ActionTerminalBase + offset1 * _tokenSymbolIndexSize, count1 * _tokenSymbolIndexSize);
        var span2 = grammarFile.Slice(ActionTerminalBase + offset2 * _tokenSymbolIndexSize, count2 * _tokenSymbolIndexSize);
        if (!span1.SequenceEqual(span2))
        {
            return false;
        }
        span1 = grammarFile.Slice(ActionBase + offset1 * _actionSize, count1 * _actionSize);
        span2 = grammarFile.Slice(ActionBase + offset2 * _actionSize, count2 * _actionSize);
        return span1.SequenceEqual(span2);
    }

    /// <summary>
    /// Returns a hash code over the <see cref="LrState.Actions"/> of the given state.
    /// </summary>
    /// <remarks>
    /// The hash code must be compared only with hash codes from the same state machine.
    /// </remarks>
    private int GetActionHashCodeUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        var (offset, count) = GetActionBoundsUnsafe(grammarFile, state);
        var hc = new HashCode();
        var actionTorminalSpan = grammarFile.Slice(ActionTerminalBase + offset * _tokenSymbolIndexSize, count * _tokenSymbolIndexSize);
        hc.AddBytes(actionTorminalSpan);
        var actionSpan = grammarFile.Slice(ActionBase + offset * _actionSize, count * _actionSize);
        hc.AddBytes(actionSpan);
        hc.Add(count);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Returns the number of empty nonterminal slots (gaps) between the first and the last goto for the given state.
    /// </summary>
    private int GetGotoGapCountUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        var (offset, count) = GetGotoBoundsUnsafe(grammarFile, state);
        if (count == 0)
        {
            return 0;
        }
        uint previousIdx = ReadGotoNonterminal(grammarFile, offset).TableIndex;
        int gaps = 0;
        for (int i = offset + 1; i < offset + count; i++)
        {
            uint idx = ReadGotoNonterminal(grammarFile, i).TableIndex;
            gaps += (int)(idx - previousIdx - 1);
            previousIdx = idx;
        }
        return gaps;
    }

    /// <summary>
    /// Returns whether the two given states have the same <see cref="LrState.Gotos"/>.
    /// </summary>
    private bool AreGotosEqualUnsafe(ReadOnlySpan<byte> grammarFile, int state1, int state2)
    {
        var (offset1, count1) = GetGotoBoundsUnsafe(grammarFile, state1);
        var (offset2, count2) = GetGotoBoundsUnsafe(grammarFile, state2);
        if (count1 != count2)
        {
            return false;
        }
        var span1 = grammarFile.Slice(GotoNonterminalBase + offset1 * _nonterminalIndexSize, count1 * _nonterminalIndexSize);
        var span2 = grammarFile.Slice(GotoNonterminalBase + offset2 * _nonterminalIndexSize, count2 * _nonterminalIndexSize);
        if (!span1.SequenceEqual(span2))
        {
            return false;
        }
        span1 = grammarFile.Slice(GotoStateBase + offset1 * _stateIndexSize, count1 * _stateIndexSize);
        span2 = grammarFile.Slice(GotoStateBase + offset2 * _stateIndexSize, count2 * _stateIndexSize);
        return span1.SequenceEqual(span2);
    }

    /// <summary>
    /// Returns a hash code over the <see cref="LrState.Gotos"/> of the given state.
    /// </summary>
    /// <remarks>
    /// The hash code must be compared only with hash codes from the same state machine.
    /// </remarks>
    private int GetGotoHashCodeUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        var (offset, count) = GetGotoBoundsUnsafe(grammarFile, state);
        var hc = new HashCode();
        hc.AddBytes(grammarFile.Slice(GotoNonterminalBase + offset * _nonterminalIndexSize, count * _nonterminalIndexSize));
        hc.AddBytes(grammarFile.Slice(GotoStateBase + offset * _stateIndexSize, count * _stateIndexSize));
        hc.Add(count);
        return hc.ToHashCode();
    }

    internal override LrEndOfFileAction GetEndOfFileActionAt(int state)
    {
        ValidateStateIndex(state);
        return ReadEofAction(Grammar.GrammarFile, state);
    }

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
