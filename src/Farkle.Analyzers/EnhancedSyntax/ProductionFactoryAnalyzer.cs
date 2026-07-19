// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers.EnhancedSyntax;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProductionFactoryAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
        DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax,
        DiagnosticDescriptors.ProductionFactoryUnsupportedType,
        DiagnosticDescriptors.ProductionFactoryTooManyTypedGrammarSymbols,
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
            if (ProductionFactorySymbols.Create(context.Compilation) is not { } symbols)
            {
                return;
            }

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var invocation = (InvocationExpressionSyntax)syntaxContext.Node;

                ProductionFactoryGeneratorShared.AnalyzeInvocation(new(syntaxContext.ReportDiagnostic, syntaxContext.SemanticModel), symbols, invocation, syntaxContext.CancellationToken);
            }, SyntaxKind.InvocationExpression);
        });

        context.RegisterCompilationStartAction(context =>
        {
            var useEnhancedSyntaxAttributeSymbol = context.Compilation.GetTypeByMetadataName(Constants.UseEnhancedSyntaxAttributeName);
            var productionSymbol = context.Compilation.Assembly.GetTypeByMetadataName(Constants.ProductionFactoryClassName);

            if (useEnhancedSyntaxAttributeSymbol is null || productionSymbol is null)
            {
                return;
            }

            context.RegisterSyntaxNodeAction(context =>
            {
                var invocation = (InvocationExpressionSyntax)context.Node;
                var semanticModel = context.SemanticModel;

                var symbolInfo = semanticModel.GetSymbolInfo(invocation.Expression, context.CancellationToken);
                if (!SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol?.ContainingType, productionSymbol))
                {
                    return;
                }

                var enclosingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart, context.CancellationToken);

                while (enclosingSymbol is not null)
                {
                    if (enclosingSymbol.GetAttributes().Select(x => x.AttributeClass).Contains(useEnhancedSyntaxAttributeSymbol, SymbolEqualityComparer.Default))
                    {
                        return;
                    }

                    enclosingSymbol = enclosingSymbol.ContainingSymbol;
                }

                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax, invocation.GetLocation()));
            }, SyntaxKind.InvocationExpression);
        });
    }
}
