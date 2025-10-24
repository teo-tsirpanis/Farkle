// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Collections;
using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;
using System.Diagnostics;

namespace Farkle.Parser.Implementation;

internal sealed class DefaultTokenizer<TChar> : Tokenizer<TChar>, ITokenizerResumptionPoint<TChar, DefaultTokenizer<TChar>.GroupState> where TChar : unmanaged, IComparable<TChar>
{
    private readonly Grammar _grammar;
    private readonly DfaWithoutConflicts<TChar> _dfa;

    public DefaultTokenizer(Grammar grammar, Dfa<TChar> dfa)
    {
        Debug.Assert(!dfa.HasConflicts);
        _grammar = grammar;
        _dfa = (DfaWithoutConflicts<TChar>)dfa;
        _dfa.PrepareForParsing();
        // If a grammar does not have any groups, we will suspend only to return
        // to the main tokenizer entry point. Without a wrapping, it would be called
        // either way regardless of suspending.
        CanSkipChainedTokenizerWrapping = grammar.Groups.Count == 0;
    }

    /// <summary>
    /// Moves forward with tokenizing a group.
    /// </summary>
    /// <returns><see langword="true"/> if a token was found or the tokenizer failed.
    /// <see langword="false"/> if more characters are needed. In the latter case
    /// callers need to suspend.</returns>
    private bool TokenizeGroup(ref ParserInputReader<TChar> input, bool isNoise, ref ValueStack<GroupHandle> groupStack,
        ref int groupLength, ref SuspendedDfaState suspendedDfaState, out ParserDiagnostic? error)
    {
        GrammarTablesHotData hotData = new(_grammar);

        // In Farkle 6, we were tracking two positions in CharStream (the predecessor of ParserInputReader).
        // The "current position" was the position where RemainingCharacters would start from, and the
        // "starting index" was the index of the first character that we must keep in the buffer. When parsing
        // simple terminals, these indices would be the same, but when parsing groups, the starting index was
        // storing the start of the outermost group, and the current position was moving forward as the
        // characters inside the group were being read.
        // Farkle 7 simplifies this by tracking only one position, the characters before which can be discarded.
        // Therefore, we have to do some bookkeeping ourselves to keep the position without consuming it and
        // throwing it away, and use a local variable to store the remaining characters.
        ReadOnlySpan<TChar> chars = input.RemainingCharacters[groupLength..];
        while (groupStack.TryPeek(out GroupHandle currentGroup))
        {
            GroupAttributes groupAttributes = hotData.GetGroupFlags(currentGroup);
            // Check if we ran out of input.
            if (chars.IsEmpty)
            {
                // If this is the final block of input, end the group if it can end when input ends.
                // Otherwise report an error.
                if (input.IsFinalBlock)
                {
                    if ((groupAttributes & GroupAttributes.EndsOnEndOfInput) != 0)
                    {
                        groupStack.Pop();
                        continue;
                    }
                    // Consume all remaining characters to get the position at the end of input.
                    // If we are in a noise group, they are already consumed and this will do nothing.
                    input.Consume(input.RemainingCharacters.Length);
                    string groupName = _grammar.GetGroupName(currentGroup);
                    error = new(input.State.CurrentPosition, new UnexpectedEndOfInputInGroupError(groupName));
                    return true;
                }
                // If this is not the final block, we have to update the group's length and suspend.
                // This lets callers know how many characters we consumed.
                groupLength = input.RemainingCharacters.Length - chars.Length;
                error = null;
                return false;
            }
            int groupDfaState = suspendedDfaState.TryGetState(out int state) ? state : _dfa.GetStartStateForGroupImpl(currentGroup);
            DfaMatchResult matchResult = default;
            bool usedCustomDfaState = false;
            if (suspendedDfaState.HasState || groupDfaState != _dfa.StartState)
            {
                matchResult = _dfa.Match(hotData.GrammarFile, chars, input.IsFinalBlock, groupDfaState, ignoreLeadingErrors: false);
                usedCustomDfaState = true;
                if (matchResult.NeedsMoreChars)
                {
                    // If the DFA had reached an accept state, consume all characters up to that point.
                    // This lets us keep track of only the DFA's state; we will resume from the first
                    // unconsumed character, and the last accept symbol is that of the DFA state.
                    // Some characters will be given to the DFA multiple times, which might be a concern
                    // since the purpose of the custom group start states is to avoid that, but this is
                    // not expected to happen as groups start and end with literals.
                    int charsToConsume = matchResult.AcceptSymbol.HasValue ? matchResult.CharactersRead : chars.Length;
                    ConsumeInput(ref input, ref chars, charsToConsume, isNoise);
                    groupLength = input.RemainingCharacters.Length - chars.Length;
                    suspendedDfaState = SuspendedDfaState.Create(matchResult.DfaState);
                    error = null;
                    return false;
                }
                suspendedDfaState = SuspendedDfaState.None;
                if (!matchResult.AcceptSymbol.HasValue)
                {
                    // Consume the characters only if the tokenizer failed. Otherwise,
                    // they will be taken care of later.
                    ConsumeInput(ref input, ref chars, matchResult.CharactersRead, isNoise);
                    if (chars.IsEmpty)
                    {
                        continue;
                    }
                }
            }
            if (!matchResult.AcceptSymbol.HasValue)
            {
                // When inside token groups, we ignore invalid characters at
                // the beginning to avoid discarding just one and repeat the loop.
                // We limit this optimization to those that keep the end token because
                // we cannot accurately determine where the final invalid characters end
                // and the group ending starts.
                bool ignoreLeadingErrors = (groupAttributes & (GroupAttributes.AdvanceByCharacter | GroupAttributes.KeepEndToken)) == 0;
                matchResult = _dfa.Match(hotData.GrammarFile, chars, input.IsFinalBlock, _dfa.StartState, ignoreLeadingErrors);
                usedCustomDfaState = false;
            }
            if (matchResult.NeedsMoreChars)
            {
                groupLength = input.RemainingCharacters.Length - chars.Length;
                error = null;
                return false;
            }
            // The DFA found something.
            if (matchResult.AcceptSymbol is { HasValue: true } acceptSymbol)
            {
                TokenSymbolAttributes symbolAttributes = hotData.GetTokenSymbolFlags(acceptSymbol);
                // A new group begins.
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    GroupHandle newGroup = hotData.GetTokenSymbolStartedGroup(acceptSymbol);
                    if (hotData.CanGroupNest(currentGroup, newGroup))
                    {
                        ConsumeInput(ref input, ref chars, matchResult.CharactersRead, isNoise);
                        groupStack.Push(newGroup);
                        continue;
                    }
                }
                // A symbol is found that ends the current group.
                else if (acceptSymbol == hotData.GetGroupEnd(currentGroup))
                {
                    if ((groupAttributes & GroupAttributes.KeepEndToken) == 0)
                    {
                        ConsumeInput(ref input, ref chars, matchResult.CharactersRead, isNoise);
                    }
                    groupStack.Pop();
                    continue;
                }
            }
            // The existing group is continuing.
            bool consumeCharsRead = usedCustomDfaState || (groupAttributes & GroupAttributes.AdvanceByCharacter) == 0;
            ConsumeInput(ref input, ref chars, consumeCharsRead ? matchResult.CharactersRead : 1, isNoise);
        }

        groupLength = input.RemainingCharacters.Length - chars.Length;
        error = null;
        return true;

        static void ConsumeInput(ref ParserInputReader<TChar> input, ref ReadOnlySpan<TChar> chars, int count, bool isNoise)
        {
            chars = chars[count..];
            // If the outermost group is a noise group, we actually consume the input, to support discarding the characters.
            if (isNoise)
            {
                input.Consume(count);
                Debug.Assert(input.RemainingCharacters == chars);
            }
        }
    }

    private static TokenizerResult CreateToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, TokenSymbolHandle symbol, int tokenLength)
    {
        try
        {
            object? semanticValue = semanticProvider.Transform(ref input.State, symbol, input.RemainingCharacters[..tokenLength]);
            TokenizerResult result = TokenizerResult.CreateSuccess(symbol, semanticValue, input.State.CurrentPosition);
            input.Consume(tokenLength);
            return result;
        }
        // We have to catch parser application exceptions here as well, because the tokenizer is not always wrapped.
        catch (ParserApplicationException ex)
        {
            return TokenizerResult.CreateError(ex.GetErrorObject(input.State.CurrentPosition));
        }
    }

    /// <summary>
    /// Starts tokenizing a group.
    /// </summary>
    private unsafe bool StartTokenizeGroup(ref ParserInputReader<TChar> input, in GrammarTablesHotData hotData, GroupHandle group, ref int charactersRead, out ParserDiagnostic? error)
    {
        TokenSymbolHandle groupContainerSymbol = hotData.GetGroupContainer(group);
        bool isNoise = !hotData.IsTerminal(groupContainerSymbol);
        // On entry, charactersRead will contain the length of the group's start,
        // to let TokenizeGroup continue after that. Because in noise groups the
        // characters are immediately consumed, we do it here for the group start
        // characters in order to keep the tokenizer state consistent with the input reader.
        if (isNoise)
        {
            input.Consume(charactersRead);
            charactersRead = 0;
        }
        ValueStack<GroupHandle> groupStack = new(stackalloc GroupHandle[4]);
        groupStack.Push(group);
        SuspendedDfaState dfaState = SuspendedDfaState.None;
#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        // The compiler cannot prove that the stack pointers of groupStack will not leak to
        // input, so it raises an error. We convert it to a warning with the use of unsafe,
        // and suppress the warning.
        bool finished = TokenizeGroup(ref input, isNoise, ref groupStack, ref charactersRead, ref dfaState, out error);
#pragma warning restore CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        if (finished)
        {
            groupStack.Dispose();
        }
        else
        {
            input.SuspendTokenizer(this, GroupState.Create(ref groupStack, groupContainerSymbol, isNoise, charactersRead, dfaState));
        }
        return finished;
    }

    bool ITokenizerResumptionPoint<TChar, GroupState>.TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, GroupState arg, out TokenizerResult result)
    {
        ValueStack<GroupHandle> groupStack = new(arg.GroupStackState);
        int charactersRead = arg.CharactersRead;
        SuspendedDfaState dfaState = arg.DfaState;
        if (TokenizeGroup(ref input, arg.IsNoise, ref groupStack, ref charactersRead, ref dfaState, out ParserDiagnostic? error))
        {
            groupStack.Dispose();
            if (error is not null)
            {
                result = TokenizerResult.CreateError(error);
                return true;
            }
            // The group had been a noise group.
            // We either return false to give a chance to the other tokenizers in the chain
            // to run, or return to the regular tokenizer logic if we are the only tokenizer.
            if (arg.IsNoise)
            {
                if (input.IsSingleTokenizerInChain())
                {
                    return TryGetNextToken(ref input, semanticProvider, out result);
                }
                result = default;
                return false;
            }
            result = CreateToken(ref input, semanticProvider, arg.GroupContainerSymbol, charactersRead);
            return true;
        }
        input.SuspendTokenizer(this, arg.Update(ref groupStack, charactersRead, dfaState));
        result = default;
        return false;
    }

    public override bool TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result)
    {
        GrammarTablesHotData hotData = new(_grammar);
        ref ParserState state = ref input.State;
        while (true)
        {
            if (input.RemainingCharacters.IsEmpty)
            {
                result = default;
                return false;
            }

            var matchResult =
                _dfa.Match(hotData.GrammarFile, input.RemainingCharacters, input.IsFinalBlock, _dfa.StartState, ignoreLeadingErrors: false);

            if (matchResult.NeedsMoreChars)
            {
                input.SuspendTokenizer(this);
                result = default;
                return false;
            }

            if (matchResult.AcceptSymbol is { HasValue: true } acceptSymbol)
            {
                int charactersRead = matchResult.CharactersRead;
                if (hotData.IsTerminal(acceptSymbol))
                {
                    result = CreateToken(ref input, semanticProvider, acceptSymbol, charactersRead);
                    return true;
                }
                TokenSymbolAttributes symbolAttributes = hotData.GetTokenSymbolFlags(acceptSymbol);
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    GroupHandle group = hotData.GetTokenSymbolStartedGroup(acceptSymbol);
                    if (!StartTokenizeGroup(ref input, in hotData, group, ref charactersRead, out ParserDiagnostic? error))
                    {
                        result = default;
                        return false;
                    }
                    if (error is not null)
                    {
                        result = TokenizerResult.CreateError(error);
                        return true;
                    }
                    TokenSymbolHandle groupContainer = hotData.GetGroupContainer(group);
                    if (hotData.IsTerminal(groupContainer))
                    {
                        result = CreateToken(ref input, semanticProvider, groupContainer, charactersRead);
                        return true;
                    }
                    Debug.Assert(charactersRead == 0);
                }
                input.Consume(charactersRead);
                if (input.IsSingleTokenizerInChain())
                {
                    continue;
                }
                result = default;
                return false;
            }

            ReadOnlySpan<TChar> lexeme = input.RemainingCharacters[..matchResult.CharactersRead];
            string errorText = ParserUtilities.GetAbbreviatedLexicalErrorText(lexeme);
            result = TokenizerResult.CreateError(new ParserDiagnostic(state.CurrentPosition,
                new LexicalError(errorText, matchResult.DfaState)));
            return true;
        }
    }

    /// <summary>
    /// Contains the state of a suspended group tokenization operation.
    /// </summary>
    private readonly struct GroupState
    {
        public ValueStack<GroupHandle>.State GroupStackState { get; init; }
        public TokenSymbolHandle GroupContainerSymbol { get; init; }
        public bool IsNoise { get; init; }
        public int CharactersRead { get; init; }
        public SuspendedDfaState DfaState { get; init; }

        public static GroupState Create(ref ValueStack<GroupHandle> groupStack, TokenSymbolHandle groupContainerSymbol,
            bool isNoise, int charactersRead, SuspendedDfaState dfaState) => new()
            {
                GroupStackState = groupStack.ExportState(),
                GroupContainerSymbol = groupContainerSymbol,
                IsNoise = isNoise,
                CharactersRead = charactersRead,
                DfaState = dfaState,
            };

        public GroupState Update(ref ValueStack<GroupHandle> groupStack, int charactersRead,
            SuspendedDfaState dfaState) => this with
            {
                GroupStackState = groupStack.ExportState(),
                CharactersRead = charactersRead,
                DfaState = dfaState,
            };
    }

    private readonly struct SuspendedDfaState
    {
        private readonly int _value;

        private SuspendedDfaState(int value)
        {
            _value = value;
        }

        public static SuspendedDfaState None => new(-1);

        public bool HasState => _value >= 0;

        public static SuspendedDfaState Create(int state)
        {
            Debug.Assert(state >= 0);
            return new(state);
        }

        public bool TryGetState(out int state)
        {
            state = _value;
            return _value >= 0;
        }
    }
}
