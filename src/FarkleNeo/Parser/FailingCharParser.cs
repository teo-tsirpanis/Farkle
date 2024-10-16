// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Parser;

internal sealed class FailingCharParser<T> : CharParser<T>
{
    private readonly object _error;
    private readonly Grammar _grammar;

    public FailingCharParser(object error, Grammar grammar)
    {
        _error = error;
        _grammar = grammar;
        IsFailing = true;
    }

    public override void Run(ref ParserInputReader<char> input, ref ParserCompletionState<T> completionState) =>
        completionState.SetError(_error);

    internal override IGrammarProvider GetGrammarProvider() => _grammar;

    internal override Tokenizer<char> GetTokenizer() => throw new NotSupportedException();

    private protected override CharParser<TNew> WithSemanticProviderCore<TNew>(ISemanticProvider<char, TNew> semanticProvider) =>
        this as CharParser<TNew> ?? new FailingCharParser<TNew>(_error, _grammar);

    private protected override CharParser<TNew> WithSemanticProviderCore<TNew>(Func<IGrammarProvider, ISemanticProvider<char, TNew>> semanticProviderFactory) =>
        this as CharParser<TNew> ?? new FailingCharParser<TNew>(_error, _grammar);

    private protected override CharParser<T> WithTokenizerCore(Tokenizer<char> tokenizer) => this;

    private protected override CharParser<T> WithTokenizerChainCore(ReadOnlySpan<ChainedTokenizerComponent<char>> components) => this;
}
