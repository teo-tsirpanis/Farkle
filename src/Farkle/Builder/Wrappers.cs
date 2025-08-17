// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

internal class GrammarBuilderWrapper(in GrammarGlobalOptions options, IGrammarSymbol symbol) : IGrammarBuilder
{
    public readonly GrammarGlobalOptions Options = options;

    public IGrammarSymbol Symbol { get; } = symbol;

    ISymbolBase IGrammarBuilder.Symbol => Symbol.Symbol;

    public IGrammarBuilder WithOptions(in GrammarGlobalOptions options) => new GrammarBuilderWrapper(options, Symbol);
}

internal class GrammarBuilderWrapper<T>(in GrammarGlobalOptions options, IGrammarSymbol symbol) : GrammarBuilderWrapper(options, symbol), IGrammarBuilder<T>
{
    public new IGrammarBuilder<T> WithOptions(in GrammarGlobalOptions options) => new GrammarBuilderWrapper<T>(options, Symbol);
}

internal class GrammarSymbolWrapper(ISymbolBase symbol) : IGrammarSymbol<object>
{
    public string Name => Symbol.Name;

    public ISymbolBase Symbol { get; } = symbol;
}

internal sealed class GrammarSymbolWrapper<T>(ISymbolBase symbol) : GrammarSymbolWrapper(symbol), IGrammarSymbol<T>;
