// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Farkle.Analyzers.EnhancedSyntax;

public static class ProductionFactoryGeneratorShared
{
    public static bool CanHaveUseEnhancedSyntaxAttribute(SyntaxNode node) => node
        is CompilationUnitSyntax
        or ClassDeclarationSyntax
        or StructDeclarationSyntax
        or RecordDeclarationSyntax
        or InterfaceDeclarationSyntax
        or BaseMethodDeclarationSyntax
        or BaseFieldDeclarationSyntax
        or BasePropertyDeclarationSyntax
        or AccessorDeclarationSyntax
        or LocalFunctionStatementSyntax;

    public static SyntaxNode AddAttributeLists(SyntaxNode node, params AttributeListSyntax[] attributeLists)
    {
        // Move the node's leading trivia to the attribute. This places the attribute after XML documentation.
        SyntaxNode nodeWithAttribute = node.WithoutLeadingTrivia() switch
        {
            // Make sure that the syntax node types match the ones in CanHaveUseEnhancedSyntaxAttribute.
            // The syntax node singleton for [UseEnhancedSyntax] is declared in Fixers/Utilities.cs,
            // because it's using a simplifier annotation from the Workspaces assembly, and only code
            // in Fixers subdirectories is allowed to use APIs from there.
            CompilationUnitSyntax compilationUnit => compilationUnit.AddAttributeLists(attributeLists),
            MemberDeclarationSyntax memberDeclaration => memberDeclaration.AddAttributeLists(attributeLists),
            AccessorDeclarationSyntax accessorDeclaration => accessorDeclaration.AddAttributeLists(attributeLists),
            LocalFunctionStatementSyntax localFunctionStatement => localFunctionStatement.AddAttributeLists(attributeLists),
            _ => throw new InvalidOperationException($"Cannot add attribute lists to node of kind {node.Kind()}."),
        };
        return nodeWithAttribute.WithLeadingTrivia(node.GetLeadingTrivia());
    }

    public static void AnalyzeInvocation(GeneratorOrAnalyzerContext<ProductionFactoryInvocation> context,
        ProductionFactorySymbols symbols, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var semanticModel = context.SemanticModel;

        var arguments = invocation.ArgumentList.Arguments;
        // Skip parameterless invocations.
        if (arguments.Count == 0)
        {
            return;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation.Expression, cancellationToken);
        // Generate an overload even if we did not cleanly bind to Production.Create(ROS<object>).
        // This will help in at least the following cases, but there could be more:
        // 1. Some of the parameters are passed by reference. We emit an overload with by value
        //    parameters, and the compiler emits a clear diagnostic that guides the user to pass
        //    it by value.
        // 2. The invocation has generic type arguments. At this point we can only see the non-generic
        //    overload, but the arguments could be valid for an overload we will generate, so we take
        //    a leap of faith. The compiler might subsequently suggest that the type arguments are not
        //    necessary.
        if (!SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, symbols.ProductionCreateBoilerplate)
            && !symbolInfo.CandidateSymbols.Contains(symbols.ProductionCreateBoilerplate!, SymbolEqualityComparer.Default))
        {
            return;
        }

        var argumentTypes = context.AddItem is { } ? ImmutableArray.CreateBuilder<ProductionMemberType>(arguments.Count) : null;
        int arity = 0;
        const int MaxGenericParametersSupported = 16;
        for (int i = 0; i < arguments.Count; i++)
        {
            ArgumentSyntax arg = arguments[i];
            var typeInfo = semanticModel.GetTypeInfo(arg.Expression, cancellationToken);
            if (typeInfo.Type is null or IErrorTypeSymbol)
            {
                context.ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryUnsupportedType, arg.GetLocation(), i, typeInfo.Type?.ToDisplayString() ?? "<null>"));
                argumentTypes = null;
                continue;
            }

            if (IsSymbolAssignableToGeneric(typeInfo.Type, symbols.IGrammarSymbol1))
            {
                if (arity == MaxGenericParametersSupported)
                {
                    context.ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryTooManyTypedGrammarSymbols, invocation.GetLocation(), MaxGenericParametersSupported));
                    argumentTypes = null;
                    continue;
                }
                argumentTypes?.Add(ProductionMemberType.IGrammarSymbol);
                arity++;
            }
            else if (IsSymbolAssignableToGeneric(typeInfo.Type, symbols.IGrammarSymbol))
            {
                argumentTypes?.Add(ProductionMemberType.IGrammarSymbolUntyped);
            }
            // Only string supports implicit conversion from other types, because IGrammarSymbol is an interface.
            // In practice, no type will be implicitly convertible to both string and IGrammarSymbol, because IGrammarSymbol
            // cannot be implemented by user code, nor can a Farkle class implementing it can be inherited by user code.
            // We should detect this case and fail if the conversion can become ambiguous in the future.
            else if (semanticModel.Compilation.ClassifyConversion(typeInfo.Type, symbols.String).IsImplicit)
            {
                argumentTypes?.Add(ProductionMemberType.String);
            }
            else
            {
                context.ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryUnsupportedType, arg.GetLocation(), i, typeInfo.Type.ToDisplayString()));
                argumentTypes = null;
            }
        }

        if (argumentTypes is not null)
        {
            context.AddItem!.Invoke(new(argumentTypes.DrainToEquatable()));
        }
    }

    public static bool IsSymbolAssignableToGeneric(ITypeSymbol symbol, ITypeSymbol? targetType)
    {
        symbol = symbol.OriginalDefinition;
        // Type is the target type.
        return symbol.Equals(targetType, SymbolEqualityComparer.Default) ||
        // Type implements the target type.
        symbol.AllInterfaces.Any(x => x.OriginalDefinition.Equals(targetType, SymbolEqualityComparer.Default)) ||
        // Type is a generic type parameter constrained to the target type.
        (symbol is ITypeParameterSymbol { ConstraintTypes: var constraints } && constraints.Any(x => IsSymbolAssignableToGeneric(x, targetType)));
    }
}
