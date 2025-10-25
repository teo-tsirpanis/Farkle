// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithoutConflicts<TChar> : DfaImplementationBase<TChar> where TChar : unmanaged, IComparable<TChar>
{
    /// <summary>
    /// A lookup table with the next state for each ASCII character, for each state.
    /// </summary>
    /// <remarks>
    /// This field is populated by <see cref="PrepareForParsing"/>.
    /// </remarks>
    private int[][]? _asciiLookup;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char CastChar(TChar c)
    {
        if (typeof(TChar) == typeof(byte))
        {
            return (char)(byte)(object)c;
        }
        if (typeof(TChar) == typeof(char))
        {
            return (char)(object)c;
        }

        ThrowHelpers.ThrowUnsupportedCharacterException();
        return default;
    }

    private static bool IsAscii(TChar c) => CastChar(c) < StateMachineUtilities.AsciiCharacterCount;

    [SetsRequiredMembers]
    public DfaWithoutConflicts(Grammar grammar, int stateCount, int edgeCount, in GrammarStateMachines.Dfa dfa)
        : base(grammar, stateCount, edgeCount, in dfa, false)
    {
        int expectedSize =
            sizeof(uint) * 2
            + stateCount * _edgeIndexSize
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * _stateIndexSize
            + stateCount * _tokenSymbolIndexSize;

        if (dfa.DfaWithoutConflicts.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        FirstEdgeBase = dfa.DfaWithoutConflicts.Offset + sizeof(uint) * 2;
        RangeFromBase = FirstEdgeBase + stateCount * _edgeIndexSize;
        RangeToBase = RangeFromBase + edgeCount * sizeof(TChar);
        EdgeTargetBase = RangeToBase + edgeCount * sizeof(TChar);
        AcceptBase = EdgeTargetBase + edgeCount * _stateIndexSize;
    }

    internal override (int Offset, int Count) GetAcceptSymbolBounds(int state)
    {
        ValidateStateIndex(state);

        if (GetAcceptSymbol(state).HasValue)
        {
            return (state, 1);
        }

        return (0, 0);
    }

    internal override TokenSymbolHandle GetAcceptSymbolAt(int index) => GetAcceptSymbol(index);

    private TokenSymbolHandle GetAcceptSymbol(int state)
    {
        ValidateStateIndex(state);
        return ReadAcceptSymbol(Grammar.GrammarFile, state);
    }

    private int NextStateSlow(ReadOnlySpan<byte> grammarFile, int state, TChar c)
    {
        int edgeOffset = ReadFirstEdge(grammarFile, state);
        int edgeLength = (state != Count - 1 ? ReadFirstEdge(grammarFile, state + 1) : _edgeCount) - edgeOffset;

        if (edgeLength != 0)
        {
            int edge = StateMachineUtilities.BufferBinarySearch(grammarFile, RangeToBase + edgeOffset * sizeof(TChar), edgeLength, c);

            if (edge < 0)
            {
                edge = Math.Min(~edge, edgeLength - 1);
            }

            TChar cFrom = StateMachineUtilities.Read<TChar>(grammarFile, RangeFromBase + (edgeOffset + edge) * sizeof(char));
            TChar cTo = StateMachineUtilities.Read<TChar>(grammarFile, RangeToBase + (edgeOffset + edge) * sizeof(char));

            if (cFrom.CompareTo(c) <= 0 && c.CompareTo(cTo) <= 0)
            {
                return ReadState(grammarFile, EdgeTargetBase, edgeOffset + edge);
            }
        }

        if (DefaultTransitionBase != 0)
        {
            return ReadState(grammarFile, DefaultTransitionBase, state);
        }

        return -1;
    }

    /// <summary>
    /// Uses the <see cref="Dfa{TChar}"/> to match a sequence of characters.
    /// </summary>
    /// <param name="grammarFile">A span with the grammar's data</param>
    /// <param name="chars">The characters to match.</param>
    /// <param name="isFinal">Whether there will be no more characters in the
    /// input stream after <paramref name="chars"/>.</param>
    /// <param name="startState">The state to start matching from.</param>
    /// <param name="ignoreLeadingErrors">Whether to ignore lexical errors at the
    /// beginning of <paramref name="chars"/>.</param>
    internal DfaMatchResult Match(ReadOnlySpan<byte> grammarFile, ReadOnlySpan<TChar> chars, bool isFinal, int startState, bool ignoreLeadingErrors)
    {
        // PrepareForParsing must have been called before this method.
        Debug.Assert(_asciiLookup is not null);

        TokenSymbolHandle acceptSymbol = ReadAcceptSymbol(grammarFile, startState);
        int acceptSymbolLength = 0;
        int acceptSymbolState = startState;

        int currentState = startState;
        int i;
        for (i = 0; i < chars.Length; i++)
        {
            TChar c = chars[i];

            // Try fast path if the character is ASCII.
            int[] stateArray = _asciiLookup[currentState];
            int nextState = CastChar(c) < stateArray.Length ? stateArray[CastChar(c)] : NextStateSlow(grammarFile, currentState, c);

            if (nextState >= 0)
            {
                ignoreLeadingErrors = false;
                currentState = nextState;
                if (ReadAcceptSymbol(grammarFile, currentState) is { HasValue: true } s)
                {
                    acceptSymbol = s;
                    acceptSymbolLength = i + 1;
                    acceptSymbolState = currentState;
                }
            }
            else if (!ignoreLeadingErrors)
            {
                goto Return;
            }
        }

        // If this is not the final input block and the DFA can move forward, we cannot accept
        // a token. To see why, consider a JSON grammar and the tokenizer finding `184` at the
        // end of the input block. We cannot accept it, there could be more digits after it that
        // were not yet read. By contrast, if we had found `true` at the end of the block, we can
        // accept it, because there is no way for a longer token to be formed.
        if (!(isFinal || this[currentState] is { Edges.Count: 0 } and { DefaultTransition: < 0 }))
        {
            return DfaMatchResult.CreateNeedsMoreChars(acceptSymbol, acceptSymbolState, acceptSymbolLength);
        }

    Return:
        if (acceptSymbol.HasValue)
        {
            return DfaMatchResult.CreateSuccess(acceptSymbol, currentState, acceptSymbolLength);
        }
        return DfaMatchResult.CreateError(currentState, i);
    }

    /// <summary>
    /// Prepares the <see cref="Dfa{TChar}"/> to be used for parsing.
    /// This initializes some lookup tables that speed up <see cref="Match"/>.
    /// </summary>
    internal void PrepareForParsing()
    {
        _asciiLookup = CreateAsciiLookup();
    }

    internal override void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        base.ValidateContent(grammarFile, grammarTables);

        for (int state = 0; state < Count; state++)
        {
            TokenSymbolHandle acceptSymbol = ReadAcceptSymbol(grammarFile, state);
            if (acceptSymbol.HasValue)
            {
                grammarTables.ValidateHandle(acceptSymbol);
            }
        }
    }

    internal override bool StateHasConflicts(int state) => false;

    private int[][] CreateAsciiLookup()
    {
        int[][] states = new int[Count][];
        for (int i = 0; i <states.Length; i++)
        {
            DfaState<TChar> state = this[i];
            bool failsOnAllAscii =
                state.DefaultTransition == -1
                && (state.Edges.Count == 0 || !IsAscii(state.Edges[0].KeyFrom));
            if (failsOnAllAscii)
            {
                states[i] = StateMachineUtilities.DfaStateAllErrors;
            }
            else
            {
                int[] arr = new int[StateMachineUtilities.AsciiCharacterCount];
                int defaultTransition = state.DefaultTransition;
                arr.AsSpan().Fill(defaultTransition);
                foreach (DfaEdge<TChar> edge in state.Edges)
                {
                    if (!IsAscii(edge.KeyFrom))
                    {
                        break;
                    }
                    int kFrom = CastChar(edge.KeyFrom);
                    int kTo = Math.Min((int)CastChar(edge.KeyTo), StateMachineUtilities.AsciiCharacterCount - 1);
                    arr.AsSpan(kFrom, kTo - kFrom + 1).Fill(edge.Target);
                }
                states[i] = arr;
            }
        }

        return states;
    }
}
