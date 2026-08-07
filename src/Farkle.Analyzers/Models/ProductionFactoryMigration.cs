// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Farkle.Analyzers.Models;

public sealed class ProductionFactoryMigration
{
    public List<ProductionFactoryMigrationParameter> Parameters { get; } = new();

    public ProductionFactoryMigrationOptions Options { get; set; }

    public static ProductionFactoryMigration? CreateFromDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Id != DiagnosticDescriptors.SwitchToProductionFactories.Id)
        {
            return null;
        }
        var properties = diagnostic.Properties;
        if (!properties.TryGetValue(nameof(Parameters), out var parametersString) || parametersString is null)
        {
            return null;
        }
        if (!properties.TryGetValue(nameof(Options), out var optionsString) || optionsString is null || !int.TryParse(optionsString, out int options))
        {
            return null;
        }

        var migration = new ProductionFactoryMigration
        {
            Options = (ProductionFactoryMigrationOptions)options,
        };
        // TODO: Use spans when the project can target modern .NET.
        foreach (var parameterString in parametersString.Split([';']))
        {
            var parts = parameterString.Split([',']);
            if (parts is not [var spanStartString, var spanLengthString, var parameterOptionsString] ||
                !int.TryParse(spanStartString, out int spanStart) ||
                !int.TryParse(spanLengthString, out int spanLength) ||
                !int.TryParse(parameterOptionsString, out int parameterOptions))
            {
                return null;
            }
            migration.Parameters.Add(new(new(spanStart, spanLength), (ProductionFactoryParameterOptions)parameterOptions));
        }
        return migration;
    }

    private string SerializeParameters()
    {
        var sb = new StringBuilder();
        foreach (var parameter in Parameters)
        {
            if (sb.Length > 0)
            {
                sb.Append(';');
            }
            sb.Append(parameter.Span.Start);
            sb.Append(',');
            sb.Append(parameter.Span.Length);
            sb.Append(',');
            sb.Append((int)parameter.Options);
        }
        return sb.ToString();
    }

    public Diagnostic ToDiagnostic(Location location)
    {
        var propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
        propertiesBuilder.Add(nameof(Parameters), SerializeParameters());
        propertiesBuilder.Add(nameof(Options), ((int)Options).ToString());
        return Diagnostic.Create(DiagnosticDescriptors.SwitchToProductionFactories,
            location,
            properties: propertiesBuilder.ToImmutable());
    }
}

public readonly struct ProductionFactoryMigrationParameter(TextSpan span, ProductionFactoryParameterOptions options)
{
    public TextSpan Span { get; } = span;

    public ProductionFactoryParameterOptions Options { get; } = options;
}

[Flags]
public enum ProductionFactoryMigrationOptions
{
    None = 0,

    AddUseEnhancedSyntaxAttribute = 1,
}

[Flags]
public enum ProductionFactoryParameterOptions
{
    None = 0,

    /// <summary>
    /// The parameter needs to be cast to <c>Farkle.Builder.IGrammarSymbol</c>.
    /// </summary>
    CastToUntypedIGrammarSymbol = 1,
}
