// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Parser;

internal sealed class FailingTokenizer<TChar> : Tokenizer<TChar>
{
    private object _message;

    public FailingTokenizer(object message)
    {
        _message = message;
        CanSkipChainedTokenizerWrapping = true;
        IsFailing = true;
    }

    public override bool TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result)
    {
        result = TokenizerResult.CreateError(_message);
        return true;
    }
}
