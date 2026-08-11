// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Farkle.Analyzers.EnhancedSyntax.Fixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveUnnecessaryAttributeFixer)), Shared]
public sealed class RemoveUnnecessaryAttributeFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [
        DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary.Id,
    ];

    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(async (context, document, diagnostics) =>
    {
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var syntaxEditor = new SyntaxEditor(root, document.Project.Solution.Services);
        new RemoveAttributesVisitor(syntaxEditor, diagnostics.Select(d => d.Location.SourceSpan), context.CancellationToken).Visit(root);
        return document.WithSyntaxRoot(syntaxEditor.GetChangedRoot());
    }, Utilities.DefaultFixAllScopes);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var attributeSyntax = root.FindNode(context.Span).FirstAncestorOrSelf<AttributeSyntax>();
        if (attributeSyntax is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Resources.RemoveUnnecessaryAttributeFixer_Title,
                cancellationToken => RemoveUseEnhancedSyntaxAttributeAsync(context.Document, root, attributeSyntax, cancellationToken),
                nameof(RemoveUnnecessaryAttributeFixer)),
            context.Diagnostics);
    }

    private static async Task<Document> RemoveUseEnhancedSyntaxAttributeAsync(Document document, SyntaxNode root, AttributeSyntax attributeSyntax, CancellationToken cancellationToken)
    {
        var parent = (AttributeListSyntax)attributeSyntax.Parent!;

        (SyntaxNode nodeToRemove, SyntaxRemoveOptions removeOptions) = parent.Attributes is [_]
            ? ((SyntaxNode)parent, SyntaxRemoveOptions.KeepExteriorTrivia)
            : (attributeSyntax, SyntaxRemoveOptions.KeepNoTrivia);

        var newRoot = root.RemoveNode(nodeToRemove, removeOptions)!;
        return document.WithSyntaxRoot(newRoot);
    }

    private sealed class RemoveAttributesVisitor : CSharpSyntaxWalker
    {
        private readonly SyntaxEditor _syntaxEditor;

        private readonly HashSet<TextSpan> _spansToRemove;

        private readonly TextSpan _maxSpanToRemove;

        private readonly CancellationToken _cancellationToken;

        public RemoveAttributesVisitor(SyntaxEditor editor, IEnumerable<TextSpan> spansToRemove, CancellationToken cancellationToken)
        {
            _syntaxEditor = editor;
            _spansToRemove = [.. spansToRemove];
            _maxSpanToRemove = GetMaxSpan(_spansToRemove);
            _cancellationToken = cancellationToken;
        }

        private static TextSpan GetMaxSpan(IReadOnlyCollection<TextSpan> spans)
        {
            var minStart = spans.Min(s => s.Start);
            var maxEnd = spans.Max(s => s.End);
            return TextSpan.FromBounds(minStart, maxEnd);
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            if (!node.Span.OverlapsWith(_maxSpanToRemove))
            {
                // Do not recurse into nodes that don't contain any of the spans to remove.
                return;
            }
            base.DefaultVisit(node);
        }

        public override void VisitAttributeList(AttributeListSyntax node)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var attributesToKeep = SyntaxFactory.SeparatedList(node.Attributes.Where(a => !_spansToRemove.Contains(a.Span)));
            if (attributesToKeep.Count == 0)
            {
                _syntaxEditor.RemoveNode(node, SyntaxRemoveOptions.KeepExteriorTrivia);
                return;
            }

            _syntaxEditor.ReplaceNode(node, node.WithAttributes(attributes: attributesToKeep));
        }
    }
}
