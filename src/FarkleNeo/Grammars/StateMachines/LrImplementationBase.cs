// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe abstract class LrImplementationBase : LrStateMachine
{
    protected readonly byte _stateIndexSize, _actionIndexSize, _gotoIndexSize, _actionSize, _eofActionSize, _tokenSymbolIndexSize, _nonterminalIndexSize;

    private Dictionary<NonterminalHandle, int>[]? _gotoLookup;

    protected int ActionCount { get; }

    protected int GotoCount { get; }

    public required int FirstActionBase { get; init; }

    public required int ActionTerminalBase { get; init; }

    public required int ActionBase { get; init; }

    public required int EofActionBase { get; init; }

    public required int FirstGotoBase { get; init; }

    public required int GotoNonterminalBase { get; init; }

    public required int GotoStateBase { get; init; }

    protected LrImplementationBase(Grammar grammar, int stateCount, int actionCount, int gotoCount, in GrammarTables grammarTables, bool hasConflicts) : base(stateCount, hasConflicts)
    {
        _stateIndexSize = GrammarUtilities.GetCompressedIndexSize(stateCount);
        _actionIndexSize = GrammarUtilities.GetCompressedIndexSize(actionCount);
        _gotoIndexSize = GrammarUtilities.GetCompressedIndexSize(gotoCount);
        _actionSize = GrammarUtilities.GetLrActionEncodedSize(stateCount, grammarTables.ProductionRowCount);
        _eofActionSize = GrammarUtilities.GetCompressedIndexSize(grammarTables.ProductionRowCount);
        _tokenSymbolIndexSize = GrammarUtilities.GetCompressedIndexSize(grammarTables.TokenSymbolRowCount);
        _nonterminalIndexSize = GrammarUtilities.GetCompressedIndexSize(grammarTables.NonterminalRowCount);

        Grammar = grammar;
        ActionCount = actionCount;
        GotoCount = gotoCount;
    }

    protected LrAction ReadAction(ReadOnlySpan<byte> grammarFile, int index) =>
        new(grammarFile.ReadIntVariableSize(ActionBase + index * _actionSize, _actionSize));

    protected LrEndOfFileAction ReadEofAction(ReadOnlySpan<byte> grammarFile, int index) =>
        new(ReadUIntVariableSizeFromArray(grammarFile, EofActionBase, index, _eofActionSize));

    protected int ReadFirstAction(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray(grammarFile, FirstActionBase, state, _actionIndexSize);

    protected int ReadFirstGoto(ReadOnlySpan<byte> grammarFile, int state) =>
        (int)ReadUIntVariableSizeFromArray(grammarFile, FirstGotoBase, state, _gotoIndexSize);

    protected int ReadGoto(ReadOnlySpan<byte> grammarFile, int index) =>
        (int)ReadUIntVariableSizeFromArray(grammarFile, GotoStateBase, index, _stateIndexSize);

    protected static uint ReadUIntVariableSizeFromArray(ReadOnlySpan<byte> grammarFile, int @base, int index, byte indexSize) =>
        grammarFile.ReadUIntVariableSize(@base + index * indexSize, indexSize);

    internal sealed override Grammar Grammar { get; }

    internal override void PrepareForParsing()
    {
        Debug.Assert(!HasConflicts);
        ReadOnlySpan<byte> grammarFile = Grammar.GrammarFile;

        var gotoLookup = new Dictionary<NonterminalHandle, int>[Count];
        for (int i = 0; i < gotoLookup.Length; i++)
        {
            var dict = new Dictionary<NonterminalHandle, int>();
            (int gotoOffset, int gotoCount) = GetGotoBoundsUnsafe(grammarFile, i);
            for (int j = 0; j < gotoCount; j++)
            {
                ((ICollection<KeyValuePair<NonterminalHandle, int>>)dict).Add(GetGotoAtUnsafe(grammarFile, gotoOffset + j));
            }
            gotoLookup[i] = dict;
        }
        _gotoLookup = gotoLookup;
    }

    internal sealed override int GetGoto(int state, NonterminalHandle nonterminal)
    {
        Debug.Assert(_gotoLookup is not null, "PrepareForParsing has not been called.");

        return _gotoLookup[state][nonterminal];
    }

    protected (int Offset, int Count) GetActionBoundsUnsafe(ReadOnlySpan<byte> grammarFile, int state)
    {
        int actionOffset = ReadFirstAction(grammarFile, state);
        int nextActionOffset = state != Count - 1 ? ReadFirstAction(grammarFile, state + 1) : ActionCount;
        return (actionOffset, nextActionOffset - actionOffset);
    }

    protected KeyValuePair<TokenSymbolHandle, LrAction> GetActionAtUnsafe(ReadOnlySpan<byte> grammarFile, int index)
    {
        TokenSymbolHandle terminal = new(ReadUIntVariableSizeFromArray(grammarFile, ActionTerminalBase, index, _tokenSymbolIndexSize));
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
        NonterminalHandle nonterminal = new(ReadUIntVariableSizeFromArray(grammarFile, GotoNonterminalBase, index, _nonterminalIndexSize));
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
        if ((uint)index >= (uint)GotoCount)
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
