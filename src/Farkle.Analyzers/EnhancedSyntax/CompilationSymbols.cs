// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace Farkle.Analyzers.EnhancedSyntax;

public sealed class ProductionFactorySymbols
{
    /// <summary>
    /// <c>Farkle.Builder.Production.ProductionBuilder.Create(params ReadOnlySpan&lt;object&gt;)</c>
    /// </summary>
    public required IMethodSymbol ProductionCreateBoilerplate { get; init; }
    /// <summary>
    /// <c>Farkle.Builder.IGrammarSymbol</c>
    /// </summary>
    public required INamedTypeSymbol? IGrammarSymbol { get; init; }
    /// <summary>
    /// <c>Farkle.Builder.IGrammarSymbol`1</c>
    /// </summary>
    public required INamedTypeSymbol? IGrammarSymbol1 { get; init; }

    private ProductionFactorySymbols() { }

    public static ProductionFactorySymbols? Create(Compilation compilation)
    {
        var productionCreateMethod = compilation.Assembly
            .GetTypeByMetadataName(Constants.ProductionFactoryClassName)
            ?.GetMembers("Create")
            .OfType<IMethodSymbol>()
            // The source generator will only see one overload, but the analyzer will also see all generated overloads.
            // We pick the one generated in post-initialization, which is the only one with a single params parameter.
            .FirstOrDefault(x => x is { Parameters: [{ IsParams: true }] });
        if (productionCreateMethod is null)
        {
            return null;
        }

        return new ProductionFactorySymbols()
        {
            IGrammarSymbol = compilation.GetTypeByMetadataName("Farkle.Builder.IGrammarSymbol"),
            IGrammarSymbol1 = compilation.GetTypeByMetadataName("Farkle.Builder.IGrammarSymbol`1"),
            ProductionCreateBoilerplate = productionCreateMethod,
        };
    }
}
