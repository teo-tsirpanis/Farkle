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
public sealed class ProductionBuilderFactoryAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
        DiagnosticDescriptors.ProductionBuilderFactoryRequiresEnhancedSyntax,
        DiagnosticDescriptors.ProductionBuilderFactoryUnsupportedType,
        DiagnosticDescriptors.ProductionBuilderFactoryTooManyTypedGrammarSymbols,
        DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary,
        DiagnosticDescriptors.CannotInferProductionBuilderFactoryParameters,
    ];

    public override void Initialize(AnalysisContext context)
    {
        if (!Debugger.IsAttached)
        {
            context.EnableConcurrentExecution();
        }
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            if (ProductionBuilderFactorySymbols.Create(context.Compilation) is not { } symbols)
            {
                return;
            }

            context.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var invocation = (InvocationExpressionSyntax)syntaxContext.Node;

                ProductionBuilderFactoryGeneratorShared.AnalyzeInvocation(new(syntaxContext.ReportDiagnostic, syntaxContext.SemanticModel), symbols, invocation, syntaxContext.CancellationToken);
            }, SyntaxKind.InvocationExpression);
        });

        context.RegisterCompilationStartAction(context =>
        {
            var useEnhancedSyntaxAttributeSymbol = context.Compilation.GetTypeByMetadataName(Constants.UseEnhancedSyntaxAttributeName);
            var productionSymbol = context.Compilation.Assembly.GetTypeByMetadataName(Constants.ProductionBuilderFactoryClassName);

            if (useEnhancedSyntaxAttributeSymbol is null || productionSymbol is null)
            {
                return;
            }

            context.RegisterSemanticModelAction(context =>
            {
                var root = context.FilterTree.GetRoot(context.CancellationToken);

                new UseEnhancedSyntaxAttributeDetector(context, useEnhancedSyntaxAttributeSymbol)
                {
                    ProductionSymbol = productionSymbol,
                }.Visit(root);
            });
        });
    }

    private sealed class UseEnhancedSyntaxAttributeDetector(SemanticModelAnalysisContext context, INamedTypeSymbol attributeSymbol) : AttributeUsageWalker(context, attributeSymbol)
    {
        public required INamedTypeSymbol ProductionSymbol { get; init; }

        protected override void ReportUnnecessaryAttribute(AttributeSyntax attribute)
        {
            if (!Context.IsGeneratedCode)
            {
                Context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary, attribute.GetLocation()));
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = Context.SemanticModel.GetSymbolInfo(node.Expression, Context.CancellationToken);
            if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol?.ContainingType, ProductionSymbol))
            {
                MarkAttributeAsUsed();
                if (!IsUnderAttribute)
                {
                    Context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ProductionBuilderFactoryRequiresEnhancedSyntax, node.Expression.GetLocation()));
                }
                else if (node.ArgumentList.Arguments.Count > 0 && symbolInfo.Symbol is IMethodSymbol { Parameters: [{ IsParams: true}] })
                {
                    Context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.CannotInferProductionBuilderFactoryParameters, node.Expression.GetLocation()));
                }
            }

            base.VisitInvocationExpression(node);
        }
    }
}
