// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Composition;
using Farkle.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

namespace Farkle.Analyzers.EnhancedSyntax.Fixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MigrateToProductionFactoryFixer)), Shared]
public sealed class MigrateToProductionFactoryFixer : CodeFixProvider
{
    private static readonly TypeSyntax s_iGrammarSymbolNode =
        SyntaxFactory.ParseTypeName($"{Constants.GlobalAlias}{Constants.IGrammarSymbolName}")
            .WithAdditionalAnnotations(Simplifier.Annotation);

    private static readonly ExpressionSyntax s_productionFactoryCreateNode =
        SyntaxFactory.ParseExpression($"{Constants.GlobalAlias}{Constants.ProductionFactoryBuildMethodFullName}")
            .WithAdditionalAnnotations(Simplifier.Annotation);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [
        DiagnosticDescriptors.SwitchToProductionFactories.Id,
    ];

    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(async (context, document, diagnostics) =>
    {
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        return await ApplyMigrationAsync(document, root, diagnostics, context.CancellationToken).ConfigureAwait(false);
    }, Utilities.DefaultFixAllScopes);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Resources.MigrateToProductionFactoryFixer_Title,
                cancellationToken => ApplyMigrationAsync(context.Document, root, context.Diagnostics, cancellationToken),
                nameof(MigrateToProductionFactoryFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> ApplyMigrationAsync(Document document, SyntaxNode root,
        ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var editor = new SyntaxEditor(root, document.Project.Solution.Services);
        HashSet<SyntaxNode> nodesWithAttributes = [];

        foreach (var diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var migration = ProductionFactoryMigration.CreateFromDiagnostic(diagnostic);
            if (migration is null)
            {
                continue;
            }
            var method = root.FindNode(diagnostic.Location.SourceSpan);
            ApplyMigration(editor, root, method, migration);
            if ((migration.Options & ProductionFactoryMigrationOptions.AddUseEnhancedSyntaxAttribute) != 0)
            {
                var attributeTarget = method.Ancestors().FirstOrDefault(ProductionFactoryGeneratorShared.CanHaveUseEnhancedSyntaxAttribute);
                if (attributeTarget is not null && nodesWithAttributes.Add(attributeTarget))
                {
                    editor.AddUseEnhancedSyntaxAttribute(attributeTarget);
                }
            }
        }

        var newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }

    private static void ApplyMigration(SyntaxEditor editor, SyntaxNode root, SyntaxNode method, ProductionFactoryMigration migration)
    {
        foreach (var parameter in migration.Parameters)
        {
            editor.TrackNode(GetExpressionNodeForParameter(parameter));
        }
        editor.ReplaceNode(method, (node, g) =>
        {
            var b = ImmutableArray.CreateBuilder<ArgumentSyntax>(migration.Parameters.Count);
            foreach (var parameter in migration.Parameters)
            {
                var paramNode = node.GetCurrentNode(GetExpressionNodeForParameter(parameter));
                if (paramNode is null)
                {
                    throw new InvalidOperationException("Tracked node not found.");
                }
                if ((parameter.Options & ProductionFactoryParameterOptions.CastToUntypedIGrammarSymbol) != 0)
                {
                    paramNode = SyntaxFactory.CastExpression(s_iGrammarSymbolNode, paramNode.WithoutTrivia()).WithTriviaFrom(paramNode);
                }
                b.Add(SyntaxFactory.Argument(paramNode));
            }
            return SyntaxFactory.InvocationExpression(
                s_productionFactoryCreateNode,
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(b.MoveToImmutable()))
            ).WithTriviaFrom(node);
        });

        ExpressionSyntax GetExpressionNodeForParameter(ProductionFactoryMigrationParameter parameter)
        {
            var paramNode = root.FindNode(parameter.Span, getInnermostNodeForTie: true).FirstAncestorOrSelf<ExpressionSyntax>();
            if (paramNode is null)
            {
                throw new InvalidOperationException("Parameter node not found.");
            }
            return paramNode;
        }
    }
}
