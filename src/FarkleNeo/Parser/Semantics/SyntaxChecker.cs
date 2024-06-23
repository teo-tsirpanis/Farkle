// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Parser.Semantics;

internal sealed class SyntaxChecker<TChar, T> : ISemanticProvider<TChar, T?>
{
    private SyntaxChecker() { }

    public static SyntaxChecker<TChar, T> Instance { get; } = new();

    public object? Fuse(ref ParserState parserState, ProductionHandle production, Span<object?> members) => null;

    public object? Transform(ref ParserState parserState, TokenSymbolHandle symbol, ReadOnlySpan<TChar> characters) => null;
}
