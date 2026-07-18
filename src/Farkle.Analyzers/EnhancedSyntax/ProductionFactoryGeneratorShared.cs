// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Farkle.Analyzers.EnhancedSyntax;

public static class ProductionFactoryGeneratorShared
{
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
        bool hasError = false;
        for (int i = 0; i < arguments.Count; i++)
        {
            ArgumentSyntax arg = arguments[i];
            var typeInfo = semanticModel.GetTypeInfo(arg.Expression, cancellationToken);
            if (typeInfo.Type is null or IErrorTypeSymbol)
            {
                return;
            }

            if (typeInfo.Type.SpecialType == SpecialType.System_String)
            {
                argumentTypes?.Add(ProductionMemberType.String);
            }
            else if (IsSymbolAssignableToGeneric(typeInfo.Type, symbols.IGrammarSymbol1))
            {
                argumentTypes?.Add(ProductionMemberType.IGrammarSymbol);
                arity++;
                if (arity == MaxGenericParametersSupported + 1)
                {
                    context.ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryTooManyTypedGrammarSymbols, invocation.GetLocation(), MaxGenericParametersSupported));
                    hasError = true;
                }
            }
            else if (IsSymbolAssignableToGeneric(typeInfo.Type, symbols.IGrammarSymbol))
            {
                argumentTypes?.Add(ProductionMemberType.IGrammarSymbolUntyped);
            }
            else
            {
                context.ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ProductionFactoryUnsupportedType, arg.GetLocation(), i, typeInfo.Type.ToDisplayString()));
                hasError = true;
            }
        }

        if (hasError)
        {
            return;
        }
        context.AddItem?.Invoke(new(argumentTypes!.DrainToEquatable()));

        static bool IsSymbolAssignableToGeneric(ITypeSymbol symbol, ITypeSymbol? targetType)
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
}
