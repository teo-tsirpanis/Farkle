// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using System.Collections.Immutable;

namespace Farkle.Analyzers.EnhancedSyntax.Fixers;

public static class Utilities
{
    private static readonly AttributeListSyntax UseEnhancedSyntaxAttributeNode =
        SyntaxFactory.AttributeList([
            SyntaxFactory.Attribute(
                SyntaxFactory.ParseName($"global::{Constants.UseEnhancedSyntaxAttributeName}")
                    .WithAdditionalAnnotations(Simplifier.Annotation)
            )
        ]);

    private static readonly AttributeListSyntax UseEnhancedSyntaxAttributeOnModuleNode =
        UseEnhancedSyntaxAttributeNode.WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.ModuleKeyword)));

    public static readonly ImmutableArray<FixAllScope> DefaultFixAllScopes = [
        FixAllScope.Document,
        FixAllScope.Project,
        FixAllScope.Solution,
        FixAllScope.ContainingMember,
        FixAllScope.ContainingType,
    ];

    extension(SyntaxNode node)
    {
        public SyntaxNode AddUseEnhancedSyntaxAttribute()
        {
            var attributeNode = node.IsKind(SyntaxKind.CompilationUnit) ? UseEnhancedSyntaxAttributeOnModuleNode : UseEnhancedSyntaxAttributeNode;
            return ProductionFactoryGeneratorShared.AddAttributeLists(node, attributeNode);
        }
    }

    extension(SyntaxEditor editor)
    {
        public void AddUseEnhancedSyntaxAttribute(SyntaxNode node)
        {
            editor.ReplaceNode(node, (n, _) => n.AddUseEnhancedSyntaxAttribute());
        }
    }
}
