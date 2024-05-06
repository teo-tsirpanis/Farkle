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

internal sealed class DefaultTokenizer<TChar> : Tokenizer<TChar>, ITokenizerResumptionPoint<TChar, DefaultTokenizer<TChar>.GroupState>
{
    private readonly Grammar _grammar;
    private readonly Dfa<TChar> _dfa;

    public DefaultTokenizer(Grammar grammar, Dfa<TChar> dfa)
    {
        Debug.Assert(!dfa.HasConflicts);
        _grammar = grammar;
        _dfa = dfa;
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
    public bool TokenizeGroup(ref ParserInputReader<TChar> input, bool isNoise, ref ValueStack<uint> groupStack, ref int groupLength, out ParserDiagnostic? error)
    {
        GrammarTablesHotData hotData = new(_grammar);

        // In Farkle 6, we were tracking two positions in CharStream (the predecessor of ParserInputReader).
        // The "current position" was the position where RemainingCharacters would start from, and the
        // "starting index" was the index of the first character that we must keep in the buffer. When parsing
        // simple terminals, these indices would be the same, but when parsing groups, the starting index was
        // storing the start of the outermost group, and the current position was moving forward as the
        // characters inside the group were being read.
        // Farkle 7 simplifies this by tracking only one position, the characters before which can be discarded.
        // Therefore we have to do some bookkeeping ourselves to keep the position without consuming it and
        // throwing it away, and use a local variable to store the remaining characters.
        ReadOnlySpan<TChar> chars = input.RemainingCharacters[groupLength..];
        while (groupStack.Count != 0)
        {
            uint currentGroup = groupStack.Peek();
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
                    string groupName = _grammar.GetString(hotData.GetGroupName(currentGroup));
                    error = new(input.State.CurrentPosition, new UnexpectedEndOfInputInGroupError(groupName));
                    return true;
                }
                // If this is not the final block, we have to update the group's length and suspend.
                groupLength = input.RemainingCharacters.Length - chars.Length;
                error = null;
                return false;
            }
            // When inside token groups, we ignore invalid characters at
            // the beginning to avoid discarding just one and repeat the loop.
            // We limit this optimization to those that keep the end token because
            // we cannot accurately determine where the final invalid characters end
            // and the group ending starts. It would be easy because group ends are
            // literal strings (except on line groups which are character groups)
            // but that's an assumption we'd better not be based on.
            bool ignoreLeadingErrors = (groupAttributes & (GroupAttributes.AdvanceByCharacter | GroupAttributes.KeepEndToken)) == 0;
            var (acceptSymbol, charactersRead, _) =
                _dfa.Match(hotData.GrammarFile, chars, input.IsFinalBlock, ignoreLeadingErrors);
            // The DFA found something.
            if (acceptSymbol.HasValue)
            {
                TokenSymbolAttributes symbolAttributes = hotData.GetTokenSymbolFlags(acceptSymbol);
                // A new group begins.
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    uint newGroup = hotData.GetTokenSymbolStartedGroup(acceptSymbol);
                    // The group is allowed to nest into this one.
                    if (hotData.CanGroupNest(currentGroup, newGroup))
                    {
                        ConsumeInput(ref input, ref chars, charactersRead, isNoise);
                        groupStack.Push(newGroup);
                        continue;
                    }
                }
                // A symbol is found that ends the current group.
                else if (acceptSymbol == hotData.GetGroupEnd(currentGroup))
                {
                    if ((groupAttributes & GroupAttributes.KeepEndToken) == 0)
                    {
                        ConsumeInput(ref input, ref chars, charactersRead, isNoise);
                    }
                    groupStack.Pop();
                    continue;
                }
            }
            // If the DFA found nothing of value and reached the end, we have to suspend and wait for more input.
            if (!input.IsFinalBlock && charactersRead == chars.Length)
            {
                groupLength = input.RemainingCharacters.Length - chars.Length;
                error = null;
                return false;
            }
            // The existing group is continuing.
            if ((groupAttributes & GroupAttributes.AdvanceByCharacter) == 0)
            {
                ConsumeInput(ref input, ref chars, charactersRead, isNoise);
            }
            else
            {
                ConsumeInput(ref input, ref chars, 1, isNoise);
                // TODO: Optimize by quickly searching for the next interesting character like in Farkle 6.
            }
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

    /// <summary>
    /// Starts tokenizing a group.
    /// </summary>
    private unsafe bool TokenizeGroup(ref ParserInputReader<TChar> input, in GrammarTablesHotData hotData, uint group, ref int charactersRead, out ParserDiagnostic? error)
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
        ValueStack<uint> groupStack = new(stackalloc uint[4]);
        groupStack.Push(group);
#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        // The compiler cannot prove that the stack pointers of groupStack will not leak to
        // input, so it raises an error. We convert it to a warning with the use of unsafe,
        // and suppress the warning.
        bool finished = TokenizeGroup(ref input, isNoise, ref groupStack, ref charactersRead, out error);
#pragma warning restore CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        if (finished)
        {
            groupStack.Dispose();
        }
        else
        {
            input.SuspendTokenizer(this, GroupState.Create(ref groupStack, groupContainerSymbol, isNoise, charactersRead));
        }
        return finished;
    }

    bool ITokenizerResumptionPoint<TChar, GroupState>.TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, GroupState arg, out TokenizerResult result)
    {
        ValueStack<uint> groupStack = new(arg.GroupStackState);
        int charactersRead = arg.CharactersRead;
        if (TokenizeGroup(ref input, arg.IsNoise, ref groupStack, ref charactersRead, out ParserDiagnostic? error))
        {
            groupStack.Dispose();
            if (error is not null)
            {
                result = TokenizerResult.CreateError(error);
                return true;
            }
            // The group had been a noise group.
            // We return to the regular tokenizer logic.
            if (arg.IsNoise)
            {
                return TryGetNextToken(ref input, semanticProvider, out result);
            }
            object? semanticValue = semanticProvider.Transform(ref input.State, arg.GroupContainerSymbol, input.RemainingCharacters[..charactersRead]);
            result = TokenizerResult.CreateSuccess(arg.GroupContainerSymbol, semanticValue, input.State.CurrentPosition);
            input.Consume(charactersRead);
            return true;
        }
        input.SuspendTokenizer(this, arg.Update(ref groupStack, charactersRead));
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

            var (acceptSymbol, charactersRead, tokenizerState) =
                _dfa.Match(hotData.GrammarFile, input.RemainingCharacters, input.IsFinalBlock, ignoreLeadingErrors: false);
            ReadOnlySpan<TChar> lexeme = input.RemainingCharacters[..charactersRead];

            if (acceptSymbol.HasValue)
            {
                if (hotData.IsTerminal(acceptSymbol))
                {
                    object? semanticValue = semanticProvider.Transform(ref state, acceptSymbol, lexeme);
                    result = TokenizerResult.CreateSuccess(acceptSymbol, semanticValue, state.CurrentPosition);
                    input.Consume(charactersRead);
                    return true;
                }
                TokenSymbolAttributes symbolAttributes = hotData.GetTokenSymbolFlags(acceptSymbol);
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    uint group = hotData.GetTokenSymbolStartedGroup(acceptSymbol);
                    if (!TokenizeGroup(ref input, in hotData, group, ref charactersRead, out ParserDiagnostic? error))
                    {
                        result = default;
                        return false;
                    }
                    if (error is not null)
                    {
                        result = TokenizerResult.CreateError(error);
                        return true;
                    }
                    if (hotData.IsTerminal(hotData.GetGroupContainer(group)))
                    {
                        object? semanticValue = semanticProvider.Transform(ref state, acceptSymbol, input.RemainingCharacters[..charactersRead]);
                        result = TokenizerResult.CreateSuccess(acceptSymbol, semanticValue, state.CurrentPosition);
                        input.Consume(charactersRead);
                        return true;
                    }
                    Debug.Assert(charactersRead == 0);
                }
                input.Consume(charactersRead);
                continue;
            }

            if (!input.IsFinalBlock && charactersRead == input.RemainingCharacters.Length)
            {
                input.SuspendTokenizer(this);
                result = default;
                return false;
            }

            string errorText = ParserUtilities.GetAbbreviatedLexicalErrorText(lexeme);
            result = TokenizerResult.CreateError(new ParserDiagnostic(state.CurrentPosition,
                new LexicalError(errorText, tokenizerState)));
            return true;
        }
    }

    /// <summary>
    /// Contains the state of a suspended group tokenization operation.
    /// </summary>
    private readonly struct GroupState
    {
        public ValueStack<uint>.State GroupStackState { get; init; }
        public TokenSymbolHandle GroupContainerSymbol { get; init; }
        public bool IsNoise { get; init; }
        public int CharactersRead { get; init; }

        private GroupState(GroupState groupState) => this = groupState;

        public static GroupState Create(ref ValueStack<uint> groupStack, TokenSymbolHandle groupContainerSymbol, bool isNoise, int charactersRead)
            => new() { GroupStackState = groupStack.ExportState(), GroupContainerSymbol = groupContainerSymbol, IsNoise = isNoise, CharactersRead = charactersRead };

        public GroupState Update(ref ValueStack<uint> groupStack, int charactersRead)
            => new(this) { GroupStackState = groupStack.ExportState(), CharactersRead = charactersRead };
    }
}
