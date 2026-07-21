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
    public static readonly AttributeSyntax UseEnhancedSyntaxAttributeNode =
        SyntaxFactory.Attribute(SyntaxFactory.ParseName($"global::{Constants.UseEnhancedSyntaxAttributeName}")
            .WithAdditionalAnnotations(Simplifier.Annotation));

    public static readonly ImmutableArray<FixAllScope> DefaultFixAllScopes = [
        FixAllScope.Document,
        FixAllScope.Project,
        FixAllScope.Solution,
        FixAllScope.ContainingMember,
        FixAllScope.ContainingType,
    ];

    extension(SyntaxEditor editor)
    {
        public void AddUseEnhancedSyntaxAttribute(SyntaxNode node)
        {
            editor.AddAttribute(node, UseEnhancedSyntaxAttributeNode);
        }
    }
}
