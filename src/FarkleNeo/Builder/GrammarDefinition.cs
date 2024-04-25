// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Diagnostics.Builder;
using Farkle.Parser;

namespace Farkle.Builder;

/// <summary>
/// Contains information about an <see cref="IGrammarBuilder"/>, decomposed into lists of symbols
/// and productions.
/// </summary>
/// <remarks>
/// This class can be used to build a <see cref="Grammars.Grammar"/> or an
/// <see cref="Parser.Semantics.ISemanticProvider{TChar, T}"/>.
/// </remarks>
internal sealed class GrammarDefinition
{
    public readonly GrammarGlobalOptions GlobalOptions;

    /// <summary>
    /// The terminals of the grammar, or the symbols that will become terminals
    /// (literals, groups, etc.).
    /// </summary>
    public required List<ISymbolBase> Terminals { get; init; }

    public required List<INonterminal> Nonterminals { get; init; }

    public required List<IProduction> Productions { get; init; }

    // Maps symbols to their names, if they are renamed.
    public required Dictionary<ISymbolBase, string> RenamedSymbols { get; init; }

    public string GrammarName => GlobalOptions.GrammarName ?? GetName(StartSymbol);

    public INonterminal StartSymbol => Nonterminals[0];

    /// <summary>
    /// An equality comparer that compares the objects returned by
    /// <see cref="GetSymbolIdentityObject"/>.
    /// </summary>
    public IEqualityComparer<object> SymbolIdentityObjectComparer =>
        Utilities.GetFallbackStringComparer(GlobalOptions.CaseSensitivity is CaseSensitivity.CaseSensitive);

    private GrammarDefinition(in GrammarGlobalOptions globalOptions)
    {
        GlobalOptions = globalOptions;
    }

    /// <summary>
    /// Gets the object that uniquely identifies an <see cref="ISymbolBase"/>.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <remarks>
    /// You must use <see cref="SymbolIdentityObjectComparer"/> to compare the objects
    /// returned by this method.
    /// </remarks>
    public static object GetSymbolIdentityObject(ISymbolBase symbol) => symbol switch
    {
        Literal literal => literal.Value,
        _ => symbol
    };

    // Unwraps a grammar builder and returns its start symbol.
    // If the grammar builder corresponds to a terminal, creates a default nonterminal
    // with the terminal in its only production.
    private static IGrammarSymbol GetStartSymbol(IGrammarBuilder grammar)
    {
        IGrammarSymbol symbol = GrammarBuilderWrapper.Unwrap(grammar);
        return symbol.Symbol is INonterminal ? symbol : new PlaceholderNonterminal(symbol.Name, symbol);
    }

    public string GetName(ISymbolBase symbol) =>
        RenamedSymbols.TryGetValue(symbol, out string? name) ? name : symbol.Name;

    public static GrammarDefinition Create(IGrammarBuilder grammar, BuilderLogger log = default, CancellationToken cancellationToken = default)
    {
        ref readonly var globalOptions = ref grammar.GetOptions();
        bool caseSensitive = globalOptions.CaseSensitivity is CaseSensitivity.CaseSensitive;
        var terminals = new List<ISymbolBase>();
        var nonterminals = new List<INonterminal>();
        var productions = new List<IProduction>();
        var renamedSymbols = new Dictionary<ISymbolBase, string>();
        var visited = new HashSet<object>(Utilities.GetFallbackStringComparer(caseSensitive));
        var nonterminalsToProcess = new Queue<INonterminal>();

        Visit(GetStartSymbol(grammar));
        while (nonterminalsToProcess.TryDequeue(out INonterminal? nonterminal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<IProduction> productionsOfNonterminal = nonterminal.FreezeAndGetProductions();
            if (productionsOfNonterminal.IsEmpty)
            {
                log.NonterminalProductionsNotSet(nonterminal.Name);
            }
            nonterminals.Add(nonterminal);
            foreach (IProduction production in productionsOfNonterminal)
            {
                productions.Add(production);
                foreach (IGrammarSymbol symbol in production.Members)
                {
                    Visit(symbol);
                }
            }
        }

        return new(in globalOptions)
        {
            Terminals = terminals,
            Nonterminals = nonterminals,
            Productions = productions,
            RenamedSymbols = renamedSymbols
        };

        void HandleRenaming(ISymbolBase symbol, string name)
        {
            if (renamedSymbols.TryGetValue(symbol, out string? existingName))
            {
                if (existingName != name)
                {
                    // TODO: Warn that the symbol is renamed more than once.
                }
                return;
            }
            renamedSymbols.Add(symbol, name);
        }

        void Visit(IGrammarSymbol symbol)
        {
            string? renamedName = (symbol as GrammarSymbolWrapper)?.RenamedName;
            ISymbolBase innerSymbol = symbol.Symbol;
            // If the symbol is renamed, add the wrapper to the visited set too, to save time.
            if (renamedName is not null && visited.Add(symbol))
            {
                HandleRenaming(innerSymbol, renamedName);
            }
            if (!visited.Add(GetSymbolIdentityObject(innerSymbol)))
            {
                return;
            }
            if (innerSymbol is INonterminal nonterminal)
            {
                nonterminalsToProcess.Enqueue(nonterminal);
            }
            else
            {
                terminals.Add(innerSymbol);
            }
        }
    }

    private sealed class PlaceholderNonterminal(string name, IGrammarSymbol symbol) : INonterminal
    {
        private readonly ImmutableArray<IProduction> _productions = [new PlaceholderProduction(symbol)];

        public string Name { get; } = name;

        public ISymbolBase Symbol => this;

        public ImmutableArray<IProduction> FreezeAndGetProductions() => _productions;

        private sealed class PlaceholderProduction(IGrammarSymbol symbol) : IProduction
        {
            public ImmutableArray<IGrammarSymbol> Members { get; } = [symbol];

            public Fuser<object?> Fuser => (ref ParserState state, Span<object?> input) => input[0];

            public object? PrecedenceToken => null;
        }
    }
}
