// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Farkle.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Farkle.Analyzers.EnhancedSyntax;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MigrateToProductionFactoryAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
        DiagnosticDescriptors.SwitchToProductionFactories,
    ];

    private const string Appended = nameof(Appended), Extended = nameof(Extended), Append = nameof(Append), Extend = nameof(Extend);

    private const string ProductionBuilderExtensions = "Farkle.Builder.ProductionBuilderExtensions";

    public override void Initialize(AnalysisContext context)
    {
        if (!Debugger.IsAttached)
        {
            context.EnableConcurrentExecution();
        }
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(context =>
        {
            var useEnhancedSyntaxAttributeSymbol = context.Compilation.GetTypeByMetadataName(Constants.UseEnhancedSyntaxAttributeName);
            var productionBuilderExtensionsSymbol = context.Compilation.GetTypeByMetadataName(ProductionBuilderExtensions);
            var iGrammarSymbol1Symbol = context.Compilation.GetTypeByMetadataName(Constants.IGrammarSymbol1Name);
            if (useEnhancedSyntaxAttributeSymbol is null || productionBuilderExtensionsSymbol is null || iGrammarSymbol1Symbol is null)
            {
                return;
            }

            var startingMembers = productionBuilderExtensionsSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(static m => m.Name is Appended or Extended)
                .ToImmutableHashSet<IMethodSymbol?>(SymbolEqualityComparer.Default);
            if (startingMembers.IsEmpty)
            {
                return;
            }

            context.RegisterSemanticModelAction(context =>
            {
                var root = context.FilterTree.GetRoot(context.CancellationToken);

                var visitor = new AttributeVisitor(context, useEnhancedSyntaxAttributeSymbol)
                {
                    StartingMembers = startingMembers,
                    IGrammarSymbol1 = iGrammarSymbol1Symbol,
                };

                visitor.Visit(root);
            });
        });
    }

    private sealed class AttributeVisitor(SemanticModelAnalysisContext context, INamedTypeSymbol attributeSymbol) : AttributeUsageWalker(context, attributeSymbol)
    {
        public required ImmutableHashSet<IMethodSymbol?> StartingMembers { get; init; }

        public required INamedTypeSymbol IGrammarSymbol1 { get; init; }

        public IAssemblySymbol FarkleAssembly => IGrammarSymbol1.ContainingAssembly;

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbol = Context.SemanticModel.GetSymbolInfo(node.Expression, Context.CancellationToken).Symbol as IMethodSymbol;
            symbol = symbol?.OriginalDefinition;
            symbol = symbol?.ReducedFrom ?? symbol;
            if (StartingMembers.Contains(symbol))
            {
                var operation = Context.SemanticModel.GetOperation(node, Context.CancellationToken) as IInvocationOperation;
                AnalyzePotentialProductionBuilderChain(operation);
            }
            base.VisitInvocationExpression(node);
        }

        private void AnalyzePotentialProductionBuilderChain(IInvocationOperation? operation)
        {
            IInvocationOperation? chain = null;

            var migration = new ProductionFactoryMigration();
            while (true)
            {
                if (operation is null)
                {
                    break;
                }

                if (!SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingAssembly, FarkleAssembly))
                {
                    break;
                }

                if (!SymbolEqualityComparer.Default.Equals(operation.Type?.ContainingAssembly, FarkleAssembly))
                {
                    break;
                }

                IArgumentOperation arg;
                if (operation.Arguments.IsEmpty)
                {
                    break;
                }
                arg = operation.Arguments[^1];
                IInvocationOperation parent;
                // Operation is of the form ExtensionMethod(parent, arg)
                if (operation is { Parent: IArgumentOperation { Parent: IInvocationOperation x }, })
                {
                    parent = x;
                }
                // Operation is of the form parent.ExtensionMethod(arg)
                else if (operation is { Parent: IInvocationOperation x2, })
                {
                    parent = x2;
                }
                else
                {
                    break;
                }

                bool isAppend;
                if (operation.TargetMethod.Name is Append or Appended)
                {
                    isAppend = true;
                }
                else if (operation.TargetMethod.Name is Extend or Extended)
                {
                    isAppend = false;
                }
                else
                {
                    break;
                }

                // Instruct the fixer to add an explicit cast to IGrammarSymbol if we call Append
                // with an argument that is implicitly convertible to IGrammarSymbol<T> for some T.
                // Otherwise the migrated code will not compile, because the source generator will
                // bind the argument to a significant member in the production builder.
                // The IDE will say that the cast is unnecessary, but there's not much we can do about
                // that. This is a secondary concern for the user to address.
                bool needsCast = isAppend
                    && arg.Value is IConversionOperation { IsImplicit: true, Operand.Type: { } t }
                    && ProductionFactoryGeneratorShared.IsSymbolAssignableToGeneric(t, IGrammarSymbol1);

                var migrationOptions = needsCast ? ProductionFactoryParameterOptions.CastToUntypedIGrammarSymbol : 0;
                migration.Parameters.Add(new(arg.Syntax.Span, migrationOptions));

                chain = operation;
                operation = parent;
            }

            if (chain is not null)
            {
                if (!IsUnderAttribute)
                {
                    migration.Options |= ProductionFactoryMigrationOptions.AddUseEnhancedSyntaxAttribute;
                }
                Context.ReportDiagnostic(migration.ToDiagnostic(chain.Syntax.GetLocation()));
            }
        }
    }
}
