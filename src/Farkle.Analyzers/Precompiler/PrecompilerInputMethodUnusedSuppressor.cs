// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers.Precompiler;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PrecompilerInputMethodUnusedSuppressor : DiagnosticSuppressor
{
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [
        DiagnosticDescriptors.PrecompilerInputMethodUnusedSuppressor,
    ];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var precompilerInputAttributeSymbol = context.Compilation.GetTypeByMetadataName(Constants.PrecompilerInputAttributeFullName);
        if (precompilerInputAttributeSymbol is null)
        {
            return;
        }

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }
            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            var semanticModel = context.GetSemanticModel(tree);
            var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
            if (symbol?.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, precompilerInputAttributeSymbol)) == true)
            {
                context.ReportSuppression(Suppression.Create(DiagnosticDescriptors.PrecompilerInputMethodUnusedSuppressor, diagnostic));
            }
        }
    }
}
