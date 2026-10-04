// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace ComSharp;

partial class ComSharpSourceGenerator
{
    private static GenerateComSharpWrappersSpec? AnalyzeGenerateComSharpWrappersAttribute(Symbols symbols, ISymbol containingSymbol,
        AttributeData attribute, Action<Diagnostic>? reportDiagnostic, CancellationToken cancellationToken)
    {
        if (!GenerateComSharpWrappersSpec.TryParse(symbols, attribute, out var spec))
        {
            return null;
        }
        // At this point, fail only if the specified type is not an interface.
        // Other errors will be reported later.
        var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken);
        bool hasError = false;
        if (!SymbolEqualityComparer.Default.Equals((containingSymbol as INamedTypeSymbol)?.BaseType, symbols.ComSharpWrappers))
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.GenerateComSharpWrappersAttribute_MustBeOnComSharpWrappers, attributeSyntax?.GetLocation()));
            hasError = true;
        }
        if (spec.Type.TypeKind != TypeKind.Interface)
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.GenerateComSharpWrappersAttribute_MustSpecifyInterface, attributeSyntax?.GetLocation()));
            hasError = true;
        }
        return hasError ? null : spec;
    }

    private static InterfaceInfo? AnalyzeInterface(Symbols symbols, INamedTypeSymbol interfaceSymbol, Action<Diagnostic>? reportDiagnostic, CancellationToken cancellationToken)
    {
        return new AnalyzeInterfaceVisitor(symbols, interfaceSymbol.ContainingAssembly!, reportDiagnostic, cancellationToken).GetInterfaceInfo(interfaceSymbol);
    }

    private static void GatherComSharpInterfaces(Symbols symbols, GenerateComSharpWrappersSpec spec, Dictionary<INamedTypeSymbol, ComSharpInterfaceOptionsSpec> visited, CancellationToken cancellationToken)
    {
        var pending = new Queue<GenerateComSharpWrappersSpec>();
        Add((INamedTypeSymbol)spec.Type, spec.Options);
        var visitor = new GatherComSharpInterfacesVisitor(symbols, spec.Type.ContainingAssembly, Add, cancellationToken);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            visitor.Visit((INamedTypeSymbol)current.Type, current.Options);
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

    private sealed class AnalyzeInterfaceVisitor(Symbols symbols, IAssemblySymbol userAssembly, Action<Diagnostic>? reportDiagnostic, CancellationToken cancellationToken)
        : ComSharpInterfaceVisitor(symbols, userAssembly, reportDiagnostic, cancellationToken)
    {
        private readonly List<ParameterMarshallingInfo> _marshallingInfos = [];

        private readonly List<ParameterInfo> _parameters = [];

        private ParameterInfo? _returnType;

        private readonly List<MemberInfo> _members = [];

        public InterfaceInfo? GetInterfaceInfo(INamedTypeSymbol interfaceSymbol)
        {
            Visit(interfaceSymbol);
            var result = HasError ? null : new InterfaceInfo(interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), _members.ToEquatableArray());
            _members.Clear();
            HasError = false;
            return result;
        }

        private ParameterMarshallingInfo PopMarshallingInfo()
        {
            var element = _marshallingInfos[^1];
            _marshallingInfos.RemoveAt(_marshallingInfos.Count - 1);
            return element;
        }

        protected override void OnInterface(INamedTypeSymbol interfaceSymbol)
        {
            base.OnInterface(interfaceSymbol);
        }

        protected override void OnMethod(IMethodSymbol methodSymbol)
        {
            base.OnMethod(methodSymbol);
            _members.Add(new MethodInfo(methodSymbol.Name, _returnType, _parameters.ToEquatableArray()));
            _returnType = null;
            _parameters.Clear();
        }

        protected override void OnProperty(IPropertySymbol propertySymbol)
        {
            base.OnProperty(propertySymbol);
            PropertyAccessors accessors = 0;
            if (propertySymbol.GetMethod is not null)
            {
                accessors |= PropertyAccessors.Get;
            }
            if (propertySymbol.SetMethod is not null)
            {
                accessors |= PropertyAccessors.Set;
            }
            Debug.Assert(_returnType is not null);
            _members.Add(new PropertyInfo(propertySymbol.Name, _returnType, accessors, _parameters.ToEquatableArray()));
            _returnType = null;
            _parameters.Clear();
        }

        protected override void OnEvent(IEventSymbol eventSymbol)
        {
            base.OnEvent(eventSymbol);
            _members.Add(new EventInfo(eventSymbol.Name, eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), PopMarshallingInfo()));
            Debug.Assert(_marshallingInfos.Count == 0);
        }

        protected override void OnParameter(IParameterSymbol parameterSymbol)
        {
            base.OnParameter(parameterSymbol);
            _parameters.Add(new(parameterSymbol.Name, parameterSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), PopMarshallingInfo()));
            Debug.Assert(_marshallingInfos.Count == 0);
        }

        protected override void OnReturnType(ITypeSymbol? type)
        {
            base.OnReturnType(type);
            if (type is null)
            {
                return;
            }
            _returnType = new ParameterInfo("<return>", type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), PopMarshallingInfo());
            Debug.Assert(_marshallingInfos.Count == 0);
        }

        protected override void OnIdentityMarshalling()
        {
            _marshallingInfos.Add(ParameterMarshallingInfo.Identity.Instance);
        }

        protected override void OnSimpleCastMarshalling(string comSharpTypeName)
        {
            _marshallingInfos.Add(new ParameterMarshallingInfo.SimpleCast(comSharpTypeName));
        }

        protected override void OnComSharpInterfaceMarshalling()
        {
            _marshallingInfos.Add(ParameterMarshallingInfo.ComSharpInterface.Instance);
        }

        protected override void OnByReferenceMarshalling(RefKind refKind)
        {
            _marshallingInfos.Add(new ParameterMarshallingInfo.ByReference(refKind));
        }

        protected override void OnGenericCollectionMarshalling(CollectionType collectionType, ITypeSymbol elementType)
        {
            var elementMarshallingInfo = PopMarshallingInfo();
            _marshallingInfos.Add(new ParameterMarshallingInfo.GenericCollection(collectionType, elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), elementMarshallingInfo));
        }

        protected override void OnTupleMarshalling(bool isValueTuple, ImmutableArray<ITypeSymbol> elementTypes)
        {
            int arity = elementTypes.Length;
            Debug.Assert(_marshallingInfos.Count >= arity);
            var elementTypeNames = elementTypes.Select(e => e.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToEquatableArray();
            var elementMarshallingInfos = _marshallingInfos
                .Skip(_marshallingInfos.Count - arity)
                .ToEquatableArray();
            _marshallingInfos.RemoveRange(_marshallingInfos.Count - arity, arity);
            _marshallingInfos.Add(new ParameterMarshallingInfo.Tuple(isValueTuple, elementTypeNames, elementMarshallingInfos));
        }
    }

    private sealed class GatherComSharpInterfacesVisitor(Symbols symbols, IAssemblySymbol userAssembly,
        Action<INamedTypeSymbol, ComSharpInterfaceOptionsSpec> onVisit, CancellationToken cancellationToken)
        : ComSharpInterfaceVisitor(symbols, userAssembly, null, cancellationToken)
    {
        private readonly Stack<ITypeSymbol> _typeStack = new();

        private ComSharpInterfaceOptionsSpec _options;

        public void Visit(INamedTypeSymbol typeSymbol, ComSharpInterfaceOptionsSpec options)
        {
            _options = options;
            Visit(typeSymbol);
        }

        protected override void OnProperty(IPropertySymbol property)
        {
            // Treat properties as distinct getters and setters. This is needed to correctly track the marshalling
            // direction if the property returns a COM# interface
            if (property.GetMethod is not null)
            {
                OnMethod(property.GetMethod);
            }
            if (property.SetMethod is not null)
            {
                OnMethod(property.SetMethod);
            }
        }

        protected override void OnReturnType(ITypeSymbol? type)
        {
            // Flip the marshalling direction for the return type.
            var oldOptions = _options;
            _options = (ComSharpInterfaceOptionsSpec)((int)_options << 1 | (int)_options >> 1) & ComSharpInterfaceOptionsSpec.Both;
            base.OnReturnType(type);
            _options = oldOptions;
        }

        protected override void OnSimpleType(ITypeSymbol typeName)
        {
            _typeStack.Push(typeName);
        }

        protected override void DefaultOnMarshalling()
        {
            CancellationToken.ThrowIfCancellationRequested();
            _typeStack.Pop();
        }

        protected override void OnComSharpInterfaceMarshalling()
        {
            onVisit((INamedTypeSymbol)_typeStack.Pop(), _options);
        }
    }
}
