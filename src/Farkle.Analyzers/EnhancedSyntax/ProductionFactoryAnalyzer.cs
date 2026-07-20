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
        DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary,
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

            context.RegisterSemanticModelAction(context =>
            {
                var root = context.FilterTree.GetRoot(context.CancellationToken);

                new UseEnhancedSyntaxAttributeDetector(context)
                {
                    UseEnhancedSyntaxAttributeSymbol = useEnhancedSyntaxAttributeSymbol,
                    ProductionSymbol = productionSymbol,
                }.Visit(root);
            });
        });
    }

    private sealed class UseEnhancedSyntaxAttributeDetector(SemanticModelAnalysisContext context) : CSharpSyntaxWalker
    {
        public required INamedTypeSymbol UseEnhancedSyntaxAttributeSymbol { get; init; }

        public required INamedTypeSymbol ProductionSymbol { get; init; }

        private int _attributeLevel, _minAttributeLevelWithInvocation;

        private void ReportUnnecessaryUseEnhancedSyntaxAttribute(AttributeSyntax attribute)
        {
            if (!context.IsGeneratedCode)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary, attribute.GetLocation()));
            }
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            if (context.FilterSpan is { } filterSpan && !filterSpan.OverlapsWith(node.Span))
            {
                return;
            }

            if (!ProductionFactoryGeneratorShared.CanHaveUseEnhancedSyntaxAttribute(node))
            {
                base.DefaultVisit(node);
                return;
            }

            AttributeSyntax? firstAttribute = null;
            foreach (var attributeList in node.ChildNodes().OfType<AttributeListSyntax>())
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var typeInfo = context.SemanticModel.GetTypeInfo(attribute, context.CancellationToken);
                    if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, UseEnhancedSyntaxAttributeSymbol))
                    {
                        if (firstAttribute != null)
                        {
                            // This level already has a UseEnhancedSyntaxAttribute.
                            ReportUnnecessaryUseEnhancedSyntaxAttribute(attribute);
                        }
                        else
                        {
                            firstAttribute = attribute;
                        }
                    }
                }
            }

            if (firstAttribute is null)
            {
                base.DefaultVisit(node);
                return;
            }

            _attributeLevel++;
            int oldMinAttributeLevelWithInvocation = _minAttributeLevelWithInvocation;
            bool isUsedAtThisLevel = false;
            foreach (var n in node.ChildNodes().Where(static n => n is not AttributeListSyntax))
            {
                _minAttributeLevelWithInvocation = int.MaxValue;
                Visit(n);
                if (_minAttributeLevelWithInvocation == _attributeLevel)
                {
                    isUsedAtThisLevel = true;
                }
            }
            if (!isUsedAtThisLevel)
            {
                ReportUnnecessaryUseEnhancedSyntaxAttribute(firstAttribute);
            }
            _minAttributeLevelWithInvocation = oldMinAttributeLevelWithInvocation;
            _attributeLevel--;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(node.Expression, context.CancellationToken);
            if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol?.ContainingType, ProductionSymbol))
            {
                _minAttributeLevelWithInvocation = Math.Min(_minAttributeLevelWithInvocation, _attributeLevel);
                if (_attributeLevel == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax, node.GetLocation()));
                }
            }

            base.VisitInvocationExpression(node);
        }
    }
}
