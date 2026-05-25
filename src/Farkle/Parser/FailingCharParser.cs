// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Parser;

internal sealed class FailingCharParser<T> : CharParser<T>
{
    private readonly object _error;
    private readonly IGrammarProvider _grammar;

    public FailingCharParser(object error, IGrammarProvider? grammar)
    {
        _error = error;
        _grammar = grammar ?? new FailingGrammarProvider(error);
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

    private sealed class FailingGrammarProvider(object error) : IGrammarProvider
    {
        public Grammar GetGrammar() => throw new InvalidOperationException(error.ToString());

        public SymbolHandle GetSymbolFromSpecialName(string specialName, bool throwIfNotFound = false) =>
            throw new InvalidOperationException(error.ToString());
    }
}
