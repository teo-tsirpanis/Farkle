// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.CodeDom.Compiler;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ComSharp;

[Generator(LanguageNames.CSharp)]
public sealed partial class ComSharpSourceGenerator : IIncrementalGenerator
{
    private static readonly (string HintName, SourceText Source)[] EmbeddedSources = GetEmbeddedSources();

    private const int GeneratedComSharpWrappersIndentLevel = 2;

    private const int GeneratedDotNetWrappersIndentLevel = 1;

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

        var wrappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            Constants.GenerateComSharpWrappersAttributeName,
            (node, _) => node.Kind() is SyntaxKind.ClassDeclaration or SyntaxKind.RecordDeclaration,
            (context, cancellationToken) =>
            {
                var symbols = Symbols.Create(context.SemanticModel.Compilation);
                if (symbols is null)
                {
                    return null;
                }
                var results = new Dictionary<INamedTypeSymbol, ComSharpInterfaceOptionsSpec>(SymbolEqualityComparer.Default);
                foreach (var attribute in context.Attributes)
                {
                    var spec = AnalyzeGenerateComSharpWrappersAttribute(symbols, context.TargetSymbol, attribute, null, cancellationToken);
                    if (spec is null)
                    {
                        continue;
                    }
                    GatherComSharpInterfaces(symbols, spec, results, cancellationToken);
                }
                if (results.Count == 0)
                {
                    return null;
                }
                var typeName = TypeOrNamespaceInfo.Create((INamespaceOrTypeSymbol)context.TargetSymbol);
                if (typeName is null)
                {
                    return null;
                }
                return new ComSharpWrappersInfo(typeName, results.Select(x => InterfaceWrappersInfo.Create(x.Key, x.Value)).ToEquatableArray());
            }
        )
            .Where(static x => x is not null)
            .Collect()
            // Group attributes declared in different parts of the same ComSharpWrappers class.
            .SelectMany((x, _) => x.GroupBy(x => x!.Type, x => x!.Interfaces, (k, xs) => new ComSharpWrappersInfo(k, xs.SelectMany(x => x).GroupBy(x => x.MetadataName).Select(x => x.Aggregate()).ToEquatableArray())));

        var interfacesToGenerate = wrappers
            .Collect()
            .SelectMany((x, _) => x
                .SelectMany(x => x.Interfaces)
                .GroupBy(item => item.MetadataName, item => item.Options)
                .Select(g => (MetadataName: g.Key, Options: g.Aggregate((x, y) => x | y))));

        IncrementalValuesProvider<(InterfaceInfo Info, ComSharpInterfaceOptionsSpec Options)> info = interfacesToGenerate
            .Combine(context.CompilationProvider)
            .Select((x, cancellationToken) =>
            {
                var symbols = Symbols.Create(x.Right)!;
                return (Info: AnalyzeInterface(symbols, x.Right.Assembly.GetTypeByMetadataName(x.Left.MetadataName)!, null, cancellationToken), x.Left.Options);
            })
            .Where(x => x.Info is not null)!;

        var wrappersSources = wrappers
            .Select((x, cancellationToken) =>
            {
                var sb = new StringBuilder();
                var w = new IndentedTextWriter(new StringWriter(sb));
                EmitComSharpWrappers(w, x, cancellationToken);
                var code = sb.ToString();
                return KeyAndValue.Create(x, code);
            });

        var comSharpWrappers = info
            .Where(x => (x.Options & ComSharpInterfaceOptionsSpec.ComSharpWrapper) != 0)
            .Select((x, _) => x.Info)
            .Select((x, cancellationToken) =>
            {
                var sb = new StringBuilder();
                var w = new IndentedTextWriter(new StringWriter(sb))
                {
                    Indent = GeneratedComSharpWrappersIndentLevel,
                };
                EmitWrapper(w, x, true, cancellationToken);
                var code = sb.ToString();
                return KeyAndValue.Create(x, code);
            });

        var dotNetWrappers = info
            .Where(x => (x.Options & ComSharpInterfaceOptionsSpec.DotNetWrapper) != 0)
            .Select((x, _) => x.Info)
            .Select((x, cancellationToken) =>
            {
                var sb = new StringBuilder();
                var w = new IndentedTextWriter(new StringWriter(sb))
                {
                    Indent = GeneratedDotNetWrappersIndentLevel,
                };
                EmitWrapper(w, x, false, cancellationToken);
                var code = sb.ToString();
                return KeyAndValue.Create(x, code);
            });

        var combined =
            wrappersSources.Collect()
                .Combine(comSharpWrappers.Collect())
                .Combine(dotNetWrappers.Collect());

        context.RegisterSourceOutput(combined, (context, source) =>
        {
            var ((wrappers, comSharpWrappers), dotNetWrappers) = source;
            if (wrappers.IsEmpty)
            {
                return;
            }
            var sb = new StringBuilder();
            var w = new IndentedTextWriter(new StringWriter(sb));
            w.WriteLine("// <auto-generated/>");
            w.WriteLine();
            w.WriteLine("#region ComSharpWrappers overrides");
            foreach (var item in wrappers)
            {
                w.Write(item.Value);
            }
            w.WriteLine("#endregion");
            w.WriteLine();
            w.WriteLine("namespace ComSharp.Generated");
            w.WriteLine('{');
            w.Indent++;
            bool first = true;
            if (!comSharpWrappers.IsEmpty)
            {
                w.WriteLineNoTabs("#region COM# callable wrappers");
                w.WriteLine("file static class GeneratedComSharpWrappers");
                w.WriteLine('{');
                w.Indent++;
                first = false;
                foreach (var item in comSharpWrappers)
                {
                    w.InnerWriter.Write(item.Value);
                }
                w.Indent--;
                w.WriteLine('}');
                w.WriteLineNoTabs("#endregion");
            }
            if (!dotNetWrappers.IsEmpty)
            {
                if (!first)
                {
                    w.InnerWriter.WriteLine();
                }
                first = false;
                w.WriteLineNoTabs("#region .NET callable wrappers");
                foreach (var item in dotNetWrappers)
                {
                    w.InnerWriter.Write(item.Value);
                }
                w.WriteLineNoTabs("#endregion");
            }
            w.Indent--;
            w.WriteLine('}');
            context.AddSource("GeneratedWrappers.g.cs", sb.ToString());
        });
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
