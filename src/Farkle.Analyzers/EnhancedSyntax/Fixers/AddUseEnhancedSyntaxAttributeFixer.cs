// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Composition;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;

namespace Farkle.Analyzers.EnhancedSyntax.Fixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddUseEnhancedSyntaxAttributeFixer)), Shared]
public class AddUseEnhancedSyntaxAttributeFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [
        DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax.Id,
    ];

    private const string AddOnDeclaringMemberKey = "AddUseEnhancedSyntaxAttribute";

    private const string AddOnDeclaringTypeKey = "AddUseEnhancedSyntaxAttributeOnDeclaringType";

    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(async (context, document, diagnostics) =>
    {
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        bool addOnMember = context.CodeActionEquivalenceKey == AddOnDeclaringMemberKey;
        if (context.Scope == (addOnMember ? FixAllScope.ContainingMember : FixAllScope.ContainingType))
        {
            // If the fix's attribute placement is the same as the fix's scope, there is only one attribute to add,
            // so we can just look at the first diagnostic.
            var declaringNode = GetParentToAddAttribute(root.FindNode(diagnostics[0].Location.SourceSpan));
            return await AddUseEnhancedSyntaxAttributeAsync(document, root, declaringNode, context.CancellationToken).ConfigureAwait(false);
        }

        var editor = new SyntaxEditor(root, document.Project.Solution.Services);
        var modifiedNodes = new HashSet<SyntaxNode>();

        foreach (var diagnostic in diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var declaringNode = GetParentToAddAttribute(root.FindNode(diagnostic.Location.SourceSpan));
            if (modifiedNodes.Add(declaringNode))
            {
                editor.AddUseEnhancedSyntaxAttribute(declaringNode);
            }
        }

        return document.WithSyntaxRoot(editor.GetChangedRoot());

        SyntaxNode GetParentToAddAttribute(SyntaxNode node) =>
            node.AncestorsAndSelf().First(x =>
                ProductionFactoryGeneratorShared.CanHaveUseEnhancedSyntaxAttribute(x)
                && (addOnMember || SyntaxFacts.IsTypeDeclaration(x.Kind())));
    }, Utilities.DefaultFixAllScopes);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var declaringMember = root.FindNode(context.Span).FirstAncestorOrSelf<SyntaxNode>(ProductionFactoryGeneratorShared.CanHaveUseEnhancedSyntaxAttribute);
        if (declaringMember is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add [UseEnhancedSyntax]",
                cancellationToken => AddUseEnhancedSyntaxAttributeAsync(context.Document, root, declaringMember, cancellationToken),
                AddOnDeclaringMemberKey),
            context.Diagnostics);

        var declaringType = declaringMember.FirstAncestorOrSelf<SyntaxNode>(x => SyntaxFacts.IsTypeDeclaration(x.Kind()));
        if (declaringType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add [UseEnhancedSyntax] on declaring type",
                cancellationToken => AddUseEnhancedSyntaxAttributeAsync(context.Document, root, declaringType, cancellationToken),
                AddOnDeclaringTypeKey),
            context.Diagnostics);
    }

    private static async Task<Document> AddUseEnhancedSyntaxAttributeAsync(Document document, SyntaxNode root, SyntaxNode declaringMember, CancellationToken cancellationToken)
    {
        var newRoot = root.ReplaceNode(declaringMember, declaringMember.AddUseEnhancedSyntaxAttribute());
        return document.WithSyntaxRoot(newRoot);
    }
}
