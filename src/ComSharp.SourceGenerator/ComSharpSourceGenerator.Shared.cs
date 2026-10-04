// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

partial class ComSharpSourceGenerator
{
    private static GenerateComSharpWrappersSpec? AnalyzeGenerateComSharpWrappersAttribute(AttributeData attribute, Action<Diagnostic>? reportDiagnostic, CancellationToken cancellationToken)
    {
        if (!GenerateComSharpWrappersSpec.TryParse(attribute, out var spec))
        {
            return null;
        }
        // At this point, fail only if the specified type is not an interface.
        // Other errors will be reported later.
        if (spec.Type.TypeKind != TypeKind.Interface)
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.GenerateComSharpWrappersAttribute_MustSpecifyInterface,
                attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken)?.GetLocation()));
            return null;
        }
        return spec;
    }

    private static void GatherComSharpInterfaces(GenerateComSharpWrappersSpec spec, Dictionary<INamedTypeSymbol, ComSharpInterfaceOptionsSpec> visited, CancellationToken cancellationToken)
    {
        var pending = new Queue<GenerateComSharpWrappersSpec>();
        Add((INamedTypeSymbol)spec.Type, spec.Options);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            foreach (var interfaceSymbol in current.Type.GetMembers())
            {
                switch (interfaceSymbol)
                {
                    case IMethodSymbol { IsStatic: false } methodSymbol:
                        HandleMethod(methodSymbol);
                        break;
                    case IPropertySymbol { IsStatic: false } propertySymbol:
                        HandleMethod(propertySymbol.GetMethod);
                        HandleMethod(propertySymbol.SetMethod);
                        break;
                    case IEventSymbol { IsStatic: false } eventSymbol:
                        HandleMethod(eventSymbol.AddMethod);
                        HandleMethod(eventSymbol.RemoveMethod);
                        break;
                }
            }
            foreach (var intf in current.Type.Interfaces)
            {
                Add(intf, current.Options);
            }

            void HandleMethod(IMethodSymbol? methodSymbol)
            {
                if (methodSymbol is null)
                {
                    return;
                }
                foreach (var parameter in methodSymbol.Parameters)
                {
                    HandleParameter(parameter.Type, isReturn: false);
                }
                HandleParameter(methodSymbol.ReturnType, isReturn: true);
            }
            void HandleParameter(ITypeSymbol paramType, bool isReturn)
            {
                if (paramType is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceSymbol)
                {
                    return;
                }
                var options = current.Options;
                if (isReturn)
                {
                    // If this is a return type, we need to flip the marshalling direction.
                    options = (ComSharpInterfaceOptionsSpec)((int)options << 1 | (int)options >> 1) & ComSharpInterfaceOptionsSpec.Both;
                }
                Add(interfaceSymbol, options);
            }
        }

        void Add(INamedTypeSymbol interfaceSymbol, ComSharpInterfaceOptionsSpec options)
        {
            if (!SymbolEqualityComparer.Default.Equals(interfaceSymbol.ContainingAssembly, spec.Type.ContainingAssembly))
            {
                // Skip interfaces from other assemblies.
                return;
            }
            if (visited.TryGetValue(interfaceSymbol, out var existingOptions))
            {
                var newOptions = options & ~existingOptions & ComSharpInterfaceOptionsSpec.Both;
                if (newOptions != 0)
                {
                    visited[interfaceSymbol] = existingOptions | newOptions;
                    pending.Enqueue(new(interfaceSymbol) { Options = newOptions });
                }
            }
            else
            {
                visited[interfaceSymbol] = options;
                pending.Enqueue(new(interfaceSymbol) { Options = options });
            }
        }
    }
}
