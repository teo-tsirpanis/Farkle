// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ComSharp;

[Generator(LanguageNames.CSharp)]
public sealed partial class ComSharpSourceGenerator : IIncrementalGenerator
{
    private static readonly (string HintName, SourceText Source)[] EmbeddedSources = GetEmbeddedSources();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddEmbeddedAttributeDefinition();
            foreach (var (hintName, source) in EmbeddedSources)
            {
                ctx.AddSource(hintName, source);
            }
        });

        var interfacesToGenerate = context.SyntaxProvider.ForAttributeWithMetadataName(
            Constants.GenerateComSharpWrappersAttributeName,
            (node, _) => node.Kind() is SyntaxKind.ClassDeclaration or SyntaxKind.RecordDeclaration,
            (context, cancellationToken) =>
            {
                var results = new Dictionary<INamedTypeSymbol, ComSharpInterfaceOptionsSpec>(SymbolEqualityComparer.Default);
                foreach (var attribute in context.Attributes)
                {
                    var spec = AnalyzeGenerateComSharpWrappersAttribute(attribute, null, cancellationToken);
                    if (spec is null)
                    {
                        continue;
                    }
                    GatherComSharpInterfaces(spec, results, cancellationToken);
                }
                return results.Select(x => (x.Key.MetadataName, x.Value)).ToImmutableArray();
            }
        )
        .Collect()
        .Select((x, _) => x
            .SelectMany(x => x)
            .GroupBy(item => item.MetadataName, item => item.Value)
            .Select(g => (MetadataName: g.Key, Options: g.Aggregate((x, y) => x | y))).ToEquatableArray());
    }

    private static (string HintName, SourceText Source)[] GetEmbeddedSources()
    {
        const string Prefix = "EmbeddedSources.";
        var asm = typeof(ComSharpSourceGenerator).Assembly;
        return asm.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix))
            .Select(name => (hintName: name[Prefix.Length..], SourceText.From(asm.GetManifestResourceStream(name)!, Encoding.UTF8, SourceHashAlgorithm.Sha256, canBeEmbedded: true)))
            .ToArray();
    }
}
