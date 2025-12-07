// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Parser.Implementation;

internal sealed class DefaultParser<T> : CharParser<T>
{
    private readonly DefaultParserImplementation<char> _implementation;

    private DefaultParser(DefaultParserImplementation<char> implementation)
    {
        _implementation = implementation;
        IsFailing = implementation.Tokenizer.IsFailing;
    }

    public DefaultParser(Grammar grammar, LrWithoutConflicts lrStateMachine, ISemanticProvider<char, T> semanticProvider, Tokenizer<char> tokenizer)
        : this(DefaultParserImplementation<char>.Create(grammar, lrStateMachine, semanticProvider, ChainedTokenizer<char>.Create(tokenizer)))
    {
    }

    public override void Run(ref ParserInputReader<char> input, ref ParserCompletionState<T> completionState)
    {
        switch (_implementation.Run(ref input, out object? resultValue))
        {
            case DefaultParserImplementation.RunResult.Success:
                completionState.SetSuccess((T)resultValue!);
                break;
            case DefaultParserImplementation.RunResult.Failure:
                Debug.Assert(resultValue is not null);
                completionState.SetError(resultValue);
                break;
        }
    }

    internal override IGrammarProvider GetGrammarProvider() => _implementation.Grammar;

    internal override Tokenizer<char> GetTokenizer() => _implementation.Tokenizer;

    private protected override CharParser<TNew> WithSemanticProviderCore<TNew>(ISemanticProvider<char, TNew> semanticProvider) =>
        new DefaultParser<TNew>(_implementation.WithSemanticProvider(semanticProvider));

    private protected override CharParser<T> WithTokenizerCore(Tokenizer<char> tokenizer) =>
        new DefaultParser<T>(_implementation.WithTokenizer(ChainedTokenizer<char>.Create(tokenizer)));
}
