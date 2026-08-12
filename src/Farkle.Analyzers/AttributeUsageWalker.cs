// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers;

/// <summary>
/// A <see cref="CSharpSyntaxWalker"/> that walks down a syntax node and keeps track of whether a
/// specific attribute is used in the node or any of its children.
/// </summary>
/// <param name="context"></param>
/// <param name="attributeSymbol"></param>
public abstract class AttributeUsageWalker(SemanticModelAnalysisContext context, INamedTypeSymbol attributeSymbol) : CSharpSyntaxWalker
{
    public INamedTypeSymbol AttributeSymbol { get; } = attributeSymbol;

    private int _attributeLevel, _minAttributeLevelWithUse;

    protected readonly SemanticModelAnalysisContext Context = context;

    protected bool IsUnderAttribute => _attributeLevel > 0;

    protected virtual void ReportUnnecessaryAttribute(AttributeSyntax attribute) { }

    protected void MarkAttributeAsUsed()
    {
        _minAttributeLevelWithUse = Math.Min(_minAttributeLevelWithUse, _attributeLevel);
    }

    public override void DefaultVisit(SyntaxNode node)
    {
        if (Context.FilterSpan is { } filterSpan && !filterSpan.OverlapsWith(node.Span))
        {
            return;
        }

        AttributeSyntax? firstAttribute = null;
        foreach (var attribute in node.ChildNodes().OfType<AttributeListSyntax>().SelectMany(static attributeList => attributeList.Attributes))
        {
            var typeInfo = Context.SemanticModel.GetTypeInfo(attribute, Context.CancellationToken);
            if (SymbolEqualityComparer.Default.Equals(typeInfo.Type, AttributeSymbol))
            {
                if (firstAttribute != null)
                {
                    // This level already has an attribute.
                    ReportUnnecessaryAttribute(attribute);
                }
                else
                {
                    firstAttribute = attribute;
                }
            }
        }

        if (firstAttribute is null)
        {
            base.DefaultVisit(node);
            return;
        }

        _attributeLevel++;
        int oldMinAttributeLevelWithUse = _minAttributeLevelWithUse;
        bool isUsedAtThisLevel = false;
        foreach (var n in node.ChildNodes().Where(static n => n is not AttributeListSyntax))
        {
            _minAttributeLevelWithUse = int.MaxValue;
            Visit(n);
            if (_minAttributeLevelWithUse == _attributeLevel)
            {
                isUsedAtThisLevel = true;
            }
        }
        if (!isUsedAtThisLevel)
        {
            ReportUnnecessaryAttribute(firstAttribute);
        }
        _minAttributeLevelWithUse = oldMinAttributeLevelWithUse;
        _attributeLevel--;
    }
}

