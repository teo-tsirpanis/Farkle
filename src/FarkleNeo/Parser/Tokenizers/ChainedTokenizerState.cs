// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Farkle.Parser.Tokenizers;

internal sealed class ChainedTokenizerState<TChar>
{
    [MemberNotNullWhen(true, nameof(TokenizerToResume))]
    public bool IsSuspended => TokenizerToResume is not null;
    /// <summary>
    /// The tokenizer to invoke upon resuming.
    /// </summary>
    public Tokenizer<TChar>? TokenizerToResume { get; set; }
    /// <summary>
    /// The index of the tokenizer chain component to continue after
    /// <see cref="TokenizerToResume"/> gets invoked.
    /// </summary>
    public int NextChainIndex { get; set; }
}
