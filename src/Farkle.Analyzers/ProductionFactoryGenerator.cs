// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Farkle.Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Farkle.Analyzers;

[Generator]
public sealed class ProductionFactoryGenerator : IIncrementalGenerator
{
    private static EquatableArray<ProductionFactoryInvocation> FindProductionFactoryInvocations(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var semanticModel = context.SemanticModel;

        var iGrammarSymbolSymbol = semanticModel.Compilation.GetTypeByMetadataName("Farkle.Builder.IGrammarSymbol");
        var iGrammarSymbol1Symbol = semanticModel.Compilation.GetTypeByMetadataName("Farkle.Builder.IGrammarSymbol`1");
        var factoryMethodSymbol = semanticModel.Compilation.GetTypeByMetadataName("Farkle.Builder.Production")?.GetMembers("Create").OfType<IMethodSymbol>().FirstOrDefault();

        if (iGrammarSymbolSymbol is null)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<ProductionFactoryInvocation>();

        foreach (var reference in context.TargetSymbol.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(cancellationToken);
            foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var arguments = invocation.ArgumentList.Arguments;
                // Skip parameterless invocations.
                if (arguments.Count == 0)
                {
                    continue;
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
                if (!SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, factoryMethodSymbol)
                    && !symbolInfo.CandidateSymbols.Contains(factoryMethodSymbol!, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var argumentTypes = ImmutableArray.CreateBuilder<ProductionMemberType>(arguments.Count);
                int arity = 0;

                foreach (var arg in arguments)
                {
                    var typeInfo = semanticModel.GetTypeInfo(arg.Expression, cancellationToken);
                    if (typeInfo.Type is null or IErrorTypeSymbol)
                    {
                        // TODO-CSHARP15: Use labeled continue.
                        goto Next;
                    }

                    if (typeInfo.Type.SpecialType == SpecialType.System_String)
                    {
                        argumentTypes.Add(ProductionMemberType.String);
                    }
                    else if (IsSymbolAssignableTo(typeInfo.Type, iGrammarSymbol1Symbol))
                    {
                        argumentTypes.Add(ProductionMemberType.IGrammarSymbol);
                        arity++;
                        if (arity > 16)
                        {
                            // Skip productions with more than 16 significant members.
                            // TODO-ANALYZER
                            goto Next;
                        }
                    }
                    else if (IsSymbolAssignableTo(typeInfo.Type, iGrammarSymbolSymbol))
                    {
                        argumentTypes.Add(ProductionMemberType.IGrammarSymbolUntyped);
                    }
                    else
                    {
                        // TODO-ANALYZER
                        goto Next;
                    }
                }

                builder.Add(new(argumentTypes.DrainToEquatable()));

            Next:;
            }
        }

        return builder.DrainToEquatable();

        static bool IsSymbolAssignableTo(ITypeSymbol symbol, ITypeSymbol? targetType)
        {
            symbol = symbol.OriginalDefinition;
            // Type is the target type.
            return symbol.Equals(targetType, SymbolEqualityComparer.Default) ||
            // Type implements the target type.
            symbol.AllInterfaces.Any(x => x.OriginalDefinition.Equals(targetType, SymbolEqualityComparer.Default)) ||
            // Type is a generic type parameter constrained to the target type.
            (symbol is ITypeParameterSymbol { ConstraintTypes: var constraints } && constraints.Any(x => IsSymbolAssignableTo(x, targetType)));
        }
    }

    private static string GetTypeName(ProductionMemberType type, int genericIdx) => type switch
    {
        ProductionMemberType.IGrammarSymbol => $"IGrammarSymbol<T{genericIdx}>",
        ProductionMemberType.IGrammarSymbolUntyped => "IGrammarSymbol",
        ProductionMemberType.String => "string",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static void WriteSourceOutput(SourceProductionContext context, EquatableArray<ProductionFactoryInvocation> invocations)
    {
        if (invocations.Count == 0)
        {
            return;
        }

        // Since we are inside the Farkle.Builder namespace, no global using directive can be used to change the meaning
        // of a type in the namespace. Therefore, we can safely reference types without qualifying them with global::Farkle.Builder.

        var sb = new StringBuilder();
        var w = new IndentedTextWriter(new StringWriter(sb), "    ");
        w.WriteLine("// <auto-generated/>");
        w.WriteLine();
        w.WriteLine("#nullable enable");
        w.WriteLine();
        w.WriteLine("namespace Farkle.Builder;");
        w.WriteLine();
        w.WriteLine("internal static partial class Production");
        using (w.EnterBlock())
        {
            foreach (var x in invocations)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                int arity = x.TypeArity;
                string extraNamespace = arity > 0 ? "ProductionBuilders." : string.Empty;
                string genericParams = arity > 0 ? $"<{string.Join(", ", Enumerable.Range(1, arity).Select(i => $"T{i}"))}>" : string.Empty;

                // Write method signature.
                w.Write($"public static {extraNamespace}ProductionBuilder{genericParams} Create{genericParams}(");
                for (int i = 0, genericIdx = 1; i < x.MemberTypes.Count; i++)
                {
                    if (i > 0)
                    {
                        w.Write(", ");
                    }
                    var type = x.MemberTypes[i];
                    w.Write(GetTypeName(type, genericIdx));
                    w.Write($" member{i}");
                    if (type == ProductionMemberType.IGrammarSymbol)
                    {
                        genericIdx++;
                    }
                }
                w.Write(") =>");

                // Write method body.
                using (w.EnterIndent())
                {
                    w.Write($"Runtime.ProductionBuilderMarshal.Create<{extraNamespace}ProductionBuilder{genericParams}>([");
                    for (int i = 0; i < x.MemberTypes.Count; i++)
                    {
                        if (i > 0)
                        {
                            w.Write(", ");
                        }
                        switch (x.MemberTypes[i])
                        {
                            case ProductionMemberType.IGrammarSymbol:
                            case ProductionMemberType.IGrammarSymbolUntyped:
                                w.Write($"member{i}");
                                break;
                            case ProductionMemberType.String:
                                w.Write($"Terminal.Literal(member{i})");
                                break;
                        }
                    }
                    w.Write("], [");
                    for (int i = 0, genericIdx = 0; i < x.MemberTypes.Count; i++)
                    {
                        if (x.MemberTypes[i] != ProductionMemberType.IGrammarSymbol)
                        {
                            continue;
                        }
                        if (genericIdx++ > 0)
                        {
                            w.Write(", ");
                        }
                        w.Write(i);
                    }
                    w.WriteLine("]);");
                }
            }
        }
        w.Flush();
        context.AddSource("Production.g.cs", sb.ToString());
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddEmbeddedAttributeDefinition();
            context.AddSource("Production.cs",
            // lang=C#
            """
            // <auto-generated/>

            namespace Farkle.Builder;

            /// <summary>
            /// Contains factory methods to conveniently create production builders.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Using this class in a method or initializer requires setting the <see cref="UseEnhancedSyntaxAttribute"/>
            /// on the member or any of its containing types.
            /// </para>
            /// <para>
            /// A source generator will detect calls to the <c>Production.Create</c> method, and generate overloads
            /// that return a production builder with the specific number of significant members.
            /// </para>
            /// </remarks>
            [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
            internal static partial class Production
            {
                public static ProductionBuilder Create(params global::System.ReadOnlySpan<object> members) => new(members);
            }

            """);
            context.AddSource("UseEnhancedSyntaxAttribute.cs",
            // lang=C#
            """
            // <auto-generated/>

            namespace Farkle.Builder;

            /// <summary>
            /// Indicates that code inside the type or member marked by this attribute can use
            /// enhanced syntax features of the Farkle API.
            /// </summary>
            /// <remarks>
            /// At this moment, this includes using  factory methods in the <see cref="Production"/>
            /// class. A source generator will detect calls to the <c>Production.Create</c> method, and generate overloads
            /// that return a production builder with the specific number of significant members.
            /// </remarks>
            [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
            [global::System.AttributeUsage(
                global::System.AttributeTargets.Class |
                global::System.AttributeTargets.Struct |
                global::System.AttributeTargets.Constructor |
                global::System.AttributeTargets.Method |
                global::System.AttributeTargets.Property |
                global::System.AttributeTargets.Field |
                global::System.AttributeTargets.Event |
                global::System.AttributeTargets.Interface,
                Inherited = false, AllowMultiple = false)]
            internal sealed class UseEnhancedSyntaxAttribute : global::System.Attribute { }

            """);
        });

        var invocations = context.SyntaxProvider.ForAttributeWithMetadataName("Farkle.Builder.UseEnhancedSyntaxAttribute",
            static (node, _) => node
                is ClassDeclarationSyntax
                or StructDeclarationSyntax
                or RecordDeclarationSyntax
                or InterfaceDeclarationSyntax
                or BaseMethodDeclarationSyntax
                or BaseFieldDeclarationSyntax
                or BasePropertyDeclarationSyntax
                or AccessorDeclarationSyntax,
            FindProductionFactoryInvocations)
            .SelectMany((invocations, _) => invocations.ToImmutableArray())
            .Collect()
            .Select((invocations, _) => invocations.Distinct().Order().ToEquatableArray());

        context.RegisterSourceOutput(invocations, WriteSourceOutput);
    }
}
