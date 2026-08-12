// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers.EnhancedSyntax;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GrammarSymbolUpcastSuppressor : DiagnosticSuppressor
{
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [DiagnosticDescriptors.IGrammarSymbolUpcastSuppressor];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var iGrammarSymbolType = context.Compilation.GetTypeByMetadataName(Constants.IGrammarSymbolName);
        var iGrammarSymbol1Type = context.Compilation.GetTypeByMetadataName(Constants.IGrammarSymbol1Name);
        var productionFactoryType = context.Compilation.GetTypeByMetadataName(Constants.ProductionFactoryClassName);

        if (iGrammarSymbolType is null || iGrammarSymbol1Type is null || productionFactoryType is null)
        {
            return;
        }

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!diagnostic.Location.IsInSource)
            {
                continue;
            }

            // The node must be a cast expression, contained within an argument of an invocation.
            // This handles cases where the cast is contained within other nodes, such as parenthesized expressions.
            var root = diagnostic.Location.SourceTree.GetRoot(context.CancellationToken);
            var cast = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as CastExpressionSyntax;
            if (cast?.FirstAncestorOrSelf<ArgumentSyntax>() is not { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } })
            {
                continue;
            }

            var semanticModel = context.GetSemanticModel(diagnostic.Location.SourceTree);

            // The invocation must be a call to a method of the production factory class.
            var invocationMethodSymbol = semanticModel.GetSymbolInfo(invocation.Expression, context.CancellationToken).Symbol;
            if (!SymbolEqualityComparer.Default.Equals(invocationMethodSymbol?.ContainingType, productionFactoryType))
            {
                continue;
            }

            // The cast must be to IGrammarSymbol.
            var castType = semanticModel.GetTypeInfo(cast.Type, context.CancellationToken).Type;
            if (!SymbolEqualityComparer.Default.Equals(castType, iGrammarSymbolType))
            {
                continue;
            }

            // The expression being cast must implement IGrammarSymbol<T> for some T.
            var expressionType = semanticModel.GetTypeInfo(cast.Expression, context.CancellationToken).Type;
            if (expressionType is null || !ProductionFactoryGeneratorShared.IsSymbolAssignableToGeneric(expressionType, iGrammarSymbol1Type))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(DiagnosticDescriptors.IGrammarSymbolUpcastSuppressor, diagnostic));
        }
    }
}
