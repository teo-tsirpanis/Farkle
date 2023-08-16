// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;
using System.Diagnostics;

namespace Farkle.Parser.Implementation;

internal sealed class DefaultTokenizer<TChar> : Tokenizer<TChar>
{
    private readonly Grammar _grammar;
    private readonly Dfa<TChar> _dfa;

    public DefaultTokenizer(Grammar grammar, Dfa<TChar> dfa)
    {
        Debug.Assert(!dfa.HasConflicts);
        _grammar = grammar;
        _dfa = dfa;
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

        if (!isFinal)
        {
            acceptSymbol = default;
        }

    Return:
        if (acceptSymbol.HasValue)
        {
            return (acceptSymbol, acceptSymbolLength, currentState);
        }
        return (default, i + 1, currentState);
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
                TokenSymbolAttributes symbolFlags = _grammar.GetTokenSymbol(acceptSymbol).Attributes;
                if ((symbolFlags & TokenSymbolAttributes.Terminal) != 0)
                {
                    object? semanticValue = semanticProvider.Transform(ref state, acceptSymbol, lexeme);
                    result = TokenizerResult.CreateSuccess(acceptSymbol, semanticValue, state.CurrentPosition);
                    input.Consume(charactersRead);
                    return true;
                }
                if ((symbolFlags & TokenSymbolAttributes.Noise) != 0)
                {
                    input.Consume(charactersRead);
                    continue;
                }
            }

            if (!input.IsFinalBlock && charactersRead == input.RemainingCharacters.Length)
            {
                input.SuspendTokenizer(this);
                result = default;
                return false;
            }

            string errorText = ParserCommon.GetAbbreviatedLexicalErrorText(lexeme);
            result = TokenizerResult.CreateError(new ParserDiagnostic(state.CurrentPosition,
                new LexicalError(errorText, tokenizerState)));
            return true;
        }
    }
}
