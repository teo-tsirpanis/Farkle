// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

internal class GrammarBuilderWrapper(in GrammarGlobalOptions options, IGrammarSymbol symbol) : IGrammarBuilder
{
    public readonly GrammarGlobalOptions Options = options;

    public IGrammarSymbol Symbol { get; } = symbol;

    public IGrammarBuilder WithOptions(in GrammarGlobalOptions options) => new GrammarBuilderWrapper(options, Symbol);
}

internal class GrammarBuilderWrapper<T>(in GrammarGlobalOptions options, IGrammarSymbol symbol) : GrammarBuilderWrapper(options, symbol), IGrammarBuilder<T>
{
    public new IGrammarBuilder<T> WithOptions(in GrammarGlobalOptions options) => new GrammarBuilderWrapper<T>(options, Symbol);
}

internal class GrammarSymbolWrapper(in GrammarSymbolOptions options, ISymbolBase symbol) : IGrammarSymbol<object>
{
    public readonly GrammarSymbolOptions Options = options;

    public string Name => Symbol.Name;

    public ISymbolBase Symbol { get; } = symbol;

    public IGrammarSymbol WithOptions(in GrammarSymbolOptions options) => new GrammarSymbolWrapper(options, Symbol);
}

internal sealed class GrammarSymbolWrapper<T>(in GrammarSymbolOptions options, ISymbolBase symbol) : GrammarSymbolWrapper(options, symbol), IGrammarSymbol<T>
{
    public new IGrammarSymbol<T> WithOptions(in GrammarSymbolOptions options) => new GrammarSymbolWrapper<T>(options, Symbol);
}
