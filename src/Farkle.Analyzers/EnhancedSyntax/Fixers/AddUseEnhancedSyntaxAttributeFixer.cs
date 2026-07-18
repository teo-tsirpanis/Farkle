// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Composition;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Simplification;

namespace Farkle.Analyzers.EnhancedSyntax.Fixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddUseEnhancedSyntaxAttributeFixer)), Shared]
public class AddUseEnhancedSyntaxAttributeFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [
        DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax.Id,
    ];

    // TODO: We could make a better fix all provider that adds the attribute to say only the whole class instead of every member, if they are many.
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaringMember = root.FindNode(diagnosticSpan).Ancestors().FirstOrDefault(x => ProductionFactoryGeneratorShared.CanHaveUseEnhancedSyntaxAttribute(x));
        if (declaringMember is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add [UseEnhancedSyntax]",
                cancellationToken => AddUseEnhancedSyntaxAttributeAsync(context.Document, declaringMember, cancellationToken),
                nameof(AddUseEnhancedSyntaxAttributeFixer)),
            diagnostic);
    }

    private static async Task<Document> AddUseEnhancedSyntaxAttributeAsync(Document document, SyntaxNode declaringMember, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::Farkle.Builder.UseEnhancedSyntax"));
        var attributeList = SyntaxFactory.AttributeList([attribute]).WithAdditionalAnnotations(Simplifier.Annotation);
        var declaringMemberWithAttribute = ProductionFactoryGeneratorShared.AddAttributeLists(declaringMember, attributeList);
        var newRoot = root.ReplaceNode(declaringMember, declaringMemberWithAttribute);
        return document.WithSyntaxRoot(newRoot);
    }
}
