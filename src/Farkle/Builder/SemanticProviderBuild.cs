// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;

namespace Farkle.Builder;

internal static class SemanticProviderBuild
{
    public static ISemanticProvider<char, T> Build<T>(GrammarDefinition grammarDefinition) =>
        new SemanticProvider<char, T>(GetTransformers(grammarDefinition.Terminals),
            GetFusers(grammarDefinition.Productions));

    private static ImmutableArray<Transformer<char, object?>> GetTransformers(List<ISymbolBase> terminals)
    {
        var builder = ImmutableArray.CreateBuilder<Transformer<char, object?>>(terminals.Count);
        foreach (var terminal in terminals)
        {
            Transformer<char, object?> transformer = terminal switch {
                Terminal x => x.Transformer,
                Group x => x.Transformer,
                _ => Transformer.GetIdentity<char, object?>()
            };
            builder.Add(transformer);
        }
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<Fuser<object?>> GetFusers(List<IProduction> productions)
    {
        var builder = ImmutableArray.CreateBuilder<Fuser<object?>>(productions.Count);
        foreach (var production in productions)
        {
            builder.Add(production.Fuser);
        }
        return builder.MoveToImmutable();
    }

    private sealed class SemanticProvider<TChar, T>(ImmutableArray<Transformer<TChar, object?>> transformers,
        ImmutableArray<Fuser<object?>> fusers) : ISemanticProvider<TChar, T>
    {
        public ImmutableArray<Transformer<TChar, object?>> Transformers { get; } = transformers;

        public ImmutableArray<Fuser<object?>> Fusers { get; } = fusers;

        public object? Fuse(ref ParserState parserState, ProductionHandle production, Span<object?> members) =>
            Fusers[production.Value](ref parserState, members);

        public object? Transform(ref ParserState parserState, TokenSymbolHandle symbol, ReadOnlySpan<TChar> characters) =>
            Transformers[symbol.Value](ref parserState, characters);
    }
}
