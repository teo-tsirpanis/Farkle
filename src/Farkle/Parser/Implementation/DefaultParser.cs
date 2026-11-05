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
        : this(new(grammar, lrStateMachine, semanticProvider, ChainedTokenizer<char>.Create(tokenizer)))
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

    internal override ParserOptions GetOptions() => _implementation.Options;

    private protected override DefaultParser<TNew> WithSemanticProviderCore<TNew>(ISemanticProvider<char, TNew> semanticProvider) =>
        new(_implementation with { SemanticProvider = semanticProvider });

    private protected override DefaultParser<T> WithTokenizerCore(Tokenizer<char> tokenizer) =>
        new(_implementation with { Tokenizer = ChainedTokenizer<char>.Create(tokenizer) });

    internal override DefaultParser<T> WithOptionsCore(ParserOptions options) =>
        _implementation.Options.Equals(options) ? this : new(_implementation with { Options = options });
}
