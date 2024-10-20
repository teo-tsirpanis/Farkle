// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithoutConflicts<TChar> : DfaImplementationBase<TChar> where TChar : unmanaged, IComparable<TChar>
{
    /// <summary>
    /// A lookup table with the next state for each ASCII character, for each starting state.
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

        throw new NotSupportedException();
    }

    private static bool IsAscii(TChar c) => CastChar(c) < StateMachineUtilities.AsciiCharacterCount;

    [SetsRequiredMembers]
    public DfaWithoutConflicts(Grammar grammar, int stateCount, int edgeCount, int tokenSymbolCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
        : base(grammar, stateCount, edgeCount, tokenSymbolCount, false)
    {
        int expectedSize =
            sizeof(uint) * 2
            + stateCount * _edgeIndexSize
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * _stateIndexSize
            + stateCount * _tokenSymbolIndexSize;

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        if (dfaDefaultTransitions.Length > 0 && dfaDefaultTransitions.Length != stateCount * _stateIndexSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        FirstEdgeBase = dfa.Offset + sizeof(uint) * 2;
        RangeFromBase = FirstEdgeBase + stateCount * _edgeIndexSize;
        RangeToBase = RangeFromBase + edgeCount * sizeof(TChar);
        EdgeTargetBase = RangeToBase + edgeCount * sizeof(TChar);
        DefaultTransitionBase = dfaDefaultTransitions.Offset;
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

    private int NextState(ReadOnlySpan<byte> grammarFile, int state, TChar c)
    {
        ValidateStateIndex(state);

        if (_asciiLookup is not null)
        {
            int[] stateArray = _asciiLookup[state];
            if (CastChar(c) < stateArray.Length)
            {
                return stateArray[CastChar(c)];
            }
        }

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

    internal override (TokenSymbolHandle AcceptSymbol, int CharactersRead, int TokenizerState)
        Match(ReadOnlySpan<byte> grammarFile, ReadOnlySpan<TChar> chars, bool isFinal, bool ignoreLeadingErrors = true)
    {
        TokenSymbolHandle acceptSymbol = default;
        int acceptSymbolLength = 0;

        int currentState = InitialState;
        int i;
        for (i = 0; i < chars.Length; i++)
        {
            TChar c = chars[i];
            int nextState = NextState(grammarFile, currentState, c);
            if (nextState >= 0)
            {
                ignoreLeadingErrors = false;
                currentState = nextState;
                if (ReadAcceptSymbol(grammarFile, currentState) is { HasValue: true } s)
                {
                    acceptSymbol = s;
                    acceptSymbolLength = i + 1;
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
        // were not yet read yet. By contrast, if we had found `true` at the end of the block, we
        // can accept it, because there is no way for a longer token to be formed.
        if (!(isFinal || this[currentState] is { Edges.Count: 0 } and { DefaultTransition: < 0 }))
        {
            acceptSymbol = default;
        }

    Return:
        if (acceptSymbol.HasValue)
        {
            return (acceptSymbol, acceptSymbolLength, currentState);
        }
        return (default, i, currentState);
    }

    internal override void PrepareForParsing()
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
