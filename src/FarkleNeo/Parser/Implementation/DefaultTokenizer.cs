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
        // If a grammar does not have any groups, we will suspend only to return
        // to the main tokenizer entry point. Without a wrapping, it would be called
        // either way regardless of suspending.
        CanSkipChainedTokenizerWrapping = grammar.Groups.Count == 0;
    }

    private (TokenSymbolHandle AcceptSymbol, int CharactersRead, int TokenizerState) TokenizeDfa(ReadOnlySpan<TChar> chars, bool isFinal, bool ignoreLeadingErrors = false)
    {
        TokenSymbolHandle acceptSymbol = default;
        int acceptSymbolLength = 0;

        int currentState = _dfa.InitialState;
        int i;
        for (i = 0; i < chars.Length; i++)
        {
            TChar c = chars[i];
            int nextState = _dfa.NextState(currentState, c);
            if (nextState >= 0)
            {
                ignoreLeadingErrors = false;
                currentState = nextState;
                if (_dfa.GetAcceptSymbol(currentState) is { HasValue: true } s)
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
        if (!(isFinal || _dfa[currentState] is { Edges.Count: 0 } and { DefaultTransition: < 0 }))
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

    /// <summary>
    /// Moves forward with tokenizing a group.
    /// </summary>
    /// <returns><see langword="true"/> if a token was found or the tokenizer failed.
    /// <see langword="false"/> if more characters are needed. In the latter case
    /// callers need to suspend.</returns>
    public bool TokenizeGroup(ref ParserInputReader<TChar> input, bool isNoise, ref ValueStack<uint> groupStack, ref int groupLength, out ParserDiagnostic? error)
    {
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
            Group currentGroup = _grammar.GetGroup(groupStack.Peek());
            GroupAttributes groupAttributes = currentGroup.Attributes;
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
                    error = new(input.State.CurrentPosition, new UnexpectedEndOfInputInGroupError(_grammar.GetString(currentGroup.Name)));
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
                TokenizeDfa(chars, input.IsFinalBlock, ignoreLeadingErrors);
            // The DFA found something.
            if (acceptSymbol.HasValue)
            {
                TokenSymbol s = _grammar.GetTokenSymbol(acceptSymbol);
                TokenSymbolAttributes symbolAttributes = s.Attributes;
                // A new group begins.
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    Group newGroup = _grammar.GetGroup(s.GetStartedGroup());
                    // The group is allowed to nest into this one.
                    if (newGroup.CanGroupNest(currentGroup.Index))
                    {
                        ConsumeInput(ref input, ref chars, charactersRead, isNoise);
                        groupStack.Push(newGroup.Index);
                        continue;
                    }
                }
                // A symbol is found that ends the current group.
                else if (acceptSymbol == currentGroup.End)
                {
                    if ((groupAttributes & GroupAttributes.KeepEndToken) != 0)
                    {
                        ConsumeInput(ref input, ref chars, charactersRead, isNoise);
                    }
                    groupStack.Pop();
                    continue;
                }
            }
            // If the DFA found nothing and reached the end, we have to suspend and wait for more input.
            if (!input.IsFinalBlock && charactersRead == chars.Length)
            {
                Debug.Assert(!acceptSymbol.HasValue);
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
    private unsafe bool TokenizeGroup(ref ParserInputReader<TChar> input, Group group, out int charactersRead, out ParserDiagnostic? error)
    {
        TokenSymbolHandle groupContainerSymbol = group.Container;
        bool isNoise = !_grammar.IsTerminal(groupContainerSymbol);
        ValueStack<uint> groupStack = new(stackalloc uint[4]);
        groupStack.Push(group.Index);
        charactersRead = 0;
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
        ref ParserState state = ref input.State;
        while (true)
        {
            if (input.RemainingCharacters.IsEmpty)
            {
                result = default;
                return false;
            }

            var (acceptSymbol, charactersRead, tokenizerState) =
                TokenizeDfa(input.RemainingCharacters, input.IsFinalBlock);
            ReadOnlySpan<TChar> lexeme = input.RemainingCharacters[..charactersRead];

            if (acceptSymbol.HasValue)
            {
                if (_grammar.IsTerminal(acceptSymbol))
                {
                    object? semanticValue = semanticProvider.Transform(ref state, acceptSymbol, lexeme);
                    result = TokenizerResult.CreateSuccess(acceptSymbol, semanticValue, state.CurrentPosition);
                    input.Consume(charactersRead);
                    return true;
                }
                TokenSymbol tokenSymbol = _grammar.GetTokenSymbol(acceptSymbol);
                TokenSymbolAttributes symbolAttributes = tokenSymbol.Attributes;
                if ((symbolAttributes & TokenSymbolAttributes.GroupStart) != 0)
                {
                    Group group = _grammar.GetGroup(tokenSymbol.GetStartedGroup());
                    if (!TokenizeGroup(ref input, group, out charactersRead, out ParserDiagnostic? error))
                    {
                        result = default;
                        return false;
                    }
                    if (error is not null)
                    {
                        result = TokenizerResult.CreateError(error);
                        return true;
                    }
                    if (_grammar.IsTerminal(group.Container))
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
