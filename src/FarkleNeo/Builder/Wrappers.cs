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

internal class GrammarSymbolWrapper(string name, ISymbolBase symbol) : IGrammarSymbol
{
    public string Name { get; } = name;
    public ISymbolBase Symbol { get; } = symbol;

    public IGrammarSymbol Rename(string name) => new GrammarSymbolWrapper(name, Symbol);
}

internal sealed class GrammarSymbolWrapper<T>(string name, ISymbolBase symbol) : GrammarSymbolWrapper(name, symbol), IGrammarSymbol<T>
{
    public new IGrammarSymbol<T> Rename(string name) => new GrammarSymbolWrapper<T>(name, Symbol);
}
