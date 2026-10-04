// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ComSharp;

public sealed partial class ComSharpSourceGenerator : IIncrementalGenerator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class Analyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
            DiagnosticDescriptors.GenerateComSharpWrappersAttribute_MustBeOnComSharpWrappers,
            DiagnosticDescriptors.GenerateComSharpWrappersAttribute_MustSpecifyInterface,
            DiagnosticDescriptors.Interface_CannotBeGeneric,
            DiagnosticDescriptors.Interface_MustHaveEmbeddedAttribute,
            DiagnosticDescriptors.Interface_MustNotBeDeclaredInMultipleParts,
            DiagnosticDescriptors.Method_CannotBeGeneric,
            DiagnosticDescriptors.Interface_MustHaveGuid,
            DiagnosticDescriptors.Method_CannotBeVararg,
            DiagnosticDescriptors.Parameter_CannotBeByReference,
            DiagnosticDescriptors.Parameter_UnsupportedType,
            DiagnosticDescriptors.Feature_Unsupported,
        ];

        public override void Initialize(AnalysisContext context)
        {
            if (!Debugger.IsAttached)
            {
                context.EnableConcurrentExecution();
            }
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var symbols = Symbols.Create(context.Compilation);
                if (symbols is null)
                {
                    return;
                }

                var analyzed = new ConcurrentDictionary<INamedTypeSymbol, object?>(SymbolEqualityComparer.Default);
                // Put interface types in a queue to be analyzed by the various symbol callbacks.
                // Usually there is one ComSharpWrappers class wrapping many interfaces.
                // Because we start our search from the ComSharpWrappers, the same callback would
                // likely analyze all interfaces.
                // Therefore, we give all callbacks that Roslyn might invoke concurrently, a chance
                // to analyze something.
                var pendingToAnalyze = new ConcurrentQueue<INamedTypeSymbol>();

                context.RegisterSymbolAction(context =>
                {
                    var symbol = (INamedTypeSymbol)context.Symbol;
                    var reportDiagnostic = context.ReportDiagnostic;
                    if (symbol.TypeKind == TypeKind.Class)
                    {
                        var visited = new Dictionary<INamedTypeSymbol, object?>(SymbolEqualityComparer.Default);
                        var pending = new Queue<GenerateComSharpWrappersSpec>();
                        var gatherVisitor = new GatherComSharpInterfacesVisitor(symbols, context.Compilation.Assembly, Add, context.CancellationToken);

                        foreach (var attr in symbol.GetAttributes())
                        {
                            if (AnalyzeGenerateComSharpWrappersAttribute(symbols, symbol, attr, reportDiagnostic, context.CancellationToken) is not { } spec)
                            {
                                continue;
                            }
                            Add((INamedTypeSymbol)spec.Type, spec.Options);
                        }

                        while (pending.Count > 0)
                        {
                            var spec = pending.Dequeue();
                            gatherVisitor.Visit((INamedTypeSymbol)spec.Type, spec.Options);
                        }

                        void Add(INamedTypeSymbol symbol, ComSharpInterfaceOptionsSpec options)
                        {
                            if (!visited.ContainsKey(symbol))
                            {
                                visited.Add(symbol, null);
                                pending.Enqueue(new(symbol) { Options = options });
                            }
                            if (analyzed.TryAdd(symbol, null))
                            {
                                pendingToAnalyze.Enqueue(symbol);
                            }
                        }
                    }

                    var visitor = new ComSharpInterfaceVisitor(symbols, context.Compilation.Assembly, context.ReportDiagnostic, context.CancellationToken);
                    while (pendingToAnalyze.TryDequeue(out var intf))
                    {
                        visitor.Visit(intf);
                    }
                }, SymbolKind.NamedType);
            });
        }
    }
}
