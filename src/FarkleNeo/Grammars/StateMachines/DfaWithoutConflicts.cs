// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using Farkle.Buffers;

namespace Farkle.Grammars.StateMachines;

internal unsafe sealed class DfaWithoutConflicts<TChar, TState, TEdge, TTokenSymbol> : DfaImplementationBase<TChar, TState, TEdge> where TChar : unmanaged, IComparable<TChar>
{
    internal required int AcceptBase { get; init; }

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

    public DfaWithoutConflicts(Grammar grammar, int stateCount, int edgeCount) : base(grammar, stateCount, edgeCount, false) { }

    public static DfaWithoutConflicts<TChar, TState, TEdge, TTokenSymbol> Create(Grammar grammar, int stateCount, int edgeCount, GrammarFileSection dfa, GrammarFileSection dfaDefaultTransitions)
    {
        int expectedSize =
            sizeof(uint) * 2
            + stateCount * sizeof(TEdge)
            + edgeCount * sizeof(TChar) * 2
            + edgeCount * sizeof(TState)
            + stateCount * sizeof(TTokenSymbol);

        if (dfa.Length != expectedSize)
        {
            ThrowHelpers.ThrowInvalidDfaDataSize();
        }

        int firstEdgeBase = dfa.Offset + sizeof(uint) * 2;
        int rangeFromBase = firstEdgeBase + stateCount * sizeof(TEdge);
        int rangeToBase = rangeFromBase + edgeCount * sizeof(TChar);
        int edgeTargetBase = rangeToBase + edgeCount * sizeof(TChar);
        int acceptBase = edgeTargetBase + edgeCount * sizeof(TState);

        if (dfaDefaultTransitions.Length > 0)
        {
            if (dfaDefaultTransitions.Length != stateCount * sizeof(TState))
            {
                ThrowHelpers.ThrowInvalidDfaDataSize();
            }
        }

        return new(grammar, stateCount, edgeCount)
        {
            FirstEdgeBase = firstEdgeBase,
            RangeFromBase = rangeFromBase,
            RangeToBase = rangeToBase,
            EdgeTargetBase = edgeTargetBase,
            DefaultTransitionBase = dfaDefaultTransitions.Offset,
            AcceptBase = acceptBase
        };
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

    private TokenSymbolHandle GetAcceptSymbolUnsafe(ReadOnlySpan<byte> grammarFile, int state) =>
        new(grammarFile.ReadUIntVariableSize<TTokenSymbol>(AcceptBase + state * sizeof(TTokenSymbol)));

    private TokenSymbolHandle GetAcceptSymbol(int state)
    {
        ValidateStateIndex(state);
        return GetAcceptSymbolUnsafe(Grammar.GrammarFile, state);
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
                return ReadState(grammarFile, EdgeTargetBase + (edgeOffset + edge) * sizeof(TState));
            }
        }

        if (DefaultTransitionBase != 0)
        {
            return ReadState(grammarFile, DefaultTransitionBase + state * sizeof(TState));
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
                if (GetAcceptSymbolUnsafe(grammarFile, currentState) is { HasValue: true } s)
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
            TokenSymbolHandle acceptSymbol = GetAcceptSymbolUnsafe(grammarFile, state);
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
