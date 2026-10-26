// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComSharp;

public class ComSharpInterfaceVisitor(Symbols symbols, IAssemblySymbol userAssembly, Action<Diagnostic>? reportDiagnostic, CancellationToken cancellationToken)
{
    private readonly Symbols _symbols = symbols;

    private readonly IAssemblySymbol _userAssembly = userAssembly;

    private SeparatedSyntaxList<SyntaxNode> _currentParameterList;

    private TypeSyntax? _currentReturnType;

    private SyntaxNode? _currentNode;

    protected Location? GetCurrentLocation() => _currentNode?.GetLocation();

    protected Action<Diagnostic>? ReportDiagnostic { get; } = reportDiagnostic;

    protected CancellationToken CancellationToken { get; } = cancellationToken;

    protected bool HasError { get; set; }

    public void Visit(INamedTypeSymbol interfaceSymbol)
    {
        HasError = false;
        InterfaceAttributes attributes = 0;
        if (interfaceSymbol.Locations.Length > 1)
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Interface_MustNotBeDeclaredInMultipleParts, interfaceSymbol.Locations[0], additionalLocations: interfaceSymbol.Locations, interfaceSymbol));
        }
        var interfaceNameToken = (interfaceSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(CancellationToken) as InterfaceDeclarationSyntax)?.Identifier ?? default;
        if (interfaceSymbol.IsGenericType)
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Interface_CannotBeGeneric, interfaceNameToken.GetLocation(), interfaceSymbol));
        }
        foreach (var baseInterface in interfaceSymbol.Interfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(baseInterface.ContainingAssembly, _userAssembly))
            {
                ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Interface_UnsupportedBaseInterface, interfaceNameToken.GetLocation(), interfaceSymbol, baseInterface));
                HasError = true;
                continue;
            }
            if ((attributes & InterfaceAttributes.ContainsBaseInterfacesWithMembers) == 0)
            {
                if (baseInterface.GetMembers().Any(x => x is { IsStatic: false } and not IMethodSymbol { MethodKind: not MethodKind.Ordinary }))
                {
                    attributes |= InterfaceAttributes.ContainsBaseInterfacesWithMembers;
                }
            }
            OnBaseInterface(baseInterface);
        }
        if (GetGuidAttributeValue(_symbols.GuidAttribute, interfaceSymbol) is null)
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Interface_MustHaveGuid, interfaceNameToken.GetLocation(), interfaceSymbol));
        }
        if (!interfaceSymbol.GetAttributes().Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, _symbols.EmbeddedAttribute)))
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Interface_MustHaveEmbeddedAttribute, interfaceNameToken.GetLocation(), interfaceSymbol));
        }
        foreach (var member in interfaceSymbol.GetMembers())
        {
            CancellationToken.ThrowIfCancellationRequested();
            _currentNode = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(CancellationToken);
            switch (member)
            {
                case IMethodSymbol { IsStatic: false, MethodKind: MethodKind.Ordinary } methodSymbol:
                    var methodSyntax = _currentNode as MethodDeclarationSyntax;
                    _currentParameterList = methodSyntax?.ParameterList?.Parameters ?? [];
                    _currentReturnType = methodSyntax?.ReturnType;
                    _currentReturnType = RemoveByRefFromType(_currentReturnType);
                    OnMethod(methodSymbol);
                    break;
                case IPropertySymbol { IsStatic: false } propertySymbol:
                    _currentReturnType = (_currentNode as BasePropertyDeclarationSyntax)?.Type;
                    _currentReturnType = RemoveByRefFromType(_currentReturnType);
                    _currentParameterList = (_currentNode as IndexerDeclarationSyntax)?.ParameterList?.Parameters ?? [];
                    OnProperty(propertySymbol);
                    break;
                case IEventSymbol { IsStatic: false } eventSymbol:
                    _currentReturnType = null;
                    _currentNode = _currentNode switch
                    {
                        EventDeclarationSyntax { Type: var type } => type,
                        VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: EventFieldDeclarationSyntax, Type: var type } } => type,
                        _ => _currentNode,
                    };
                    OnEvent(eventSymbol);
                    break;
            }
        }
        OnInterface(interfaceSymbol, attributes);

        static TypeSyntax? RemoveByRefFromType(TypeSyntax? typeSyntax) => typeSyntax switch
        {
            RefTypeSyntax { Type: var elementType } => elementType,
            _ => typeSyntax,
        };
    }

    protected virtual void OnBaseInterface(INamedTypeSymbol baseInterface) { }

    protected virtual void OnInterface(INamedTypeSymbol interfaceSymbol, InterfaceAttributes attributes) { }

    protected virtual void OnMethod(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.IsGenericMethod)
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Method_CannotBeGeneric, GetCurrentLocation(), methodSymbol));
        }
        if (methodSymbol.IsVararg)
        {
            HasError = true;
            ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Method_CannotBeVararg, GetCurrentLocation(), methodSymbol));
        }
        for (int i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            IParameterSymbol? parameter = methodSymbol.Parameters[i];
            if (i < _currentParameterList.Count)
            {
                _currentNode = _currentParameterList[i] switch
                {
                    ParameterSyntax { Type: var type } => type,
                    _ => null
                };
            }
            else
            {
                _currentNode = null;
            }
            OnParameter(parameter);
        }
        OnReturnType(methodSymbol.ReturnType is { SpecialType: not SpecialType.System_Void } rt ? rt : null, methodSymbol.RefKind);
    }

    protected virtual void OnProperty(IPropertySymbol propertySymbol)
    {
        for (int i = 0; i < propertySymbol.Parameters.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            IParameterSymbol? parameter = propertySymbol.Parameters[i];
            if (i < _currentParameterList.Count)
            {
                _currentNode = _currentParameterList[i] switch
                {
                    ParameterSyntax { Type: var type } => type,
                    _ => null
                };
            }
            else
            {
                _currentNode = null;
            }
            OnParameter(parameter);
        }
        OnReturnType(propertySymbol.Type, propertySymbol.RefKind);
    }

    protected virtual void OnEvent(IEventSymbol eventSymbol)
    {
        OnType(eventSymbol.Type);
        OnReturnType(null, RefKind.None);
    }

    protected virtual void OnReturnType(ITypeSymbol? type, RefKind refKind)
    {
        if (type is null)
        {
            return;
        }
        _currentNode = _currentReturnType;
        if (refKind != RefKind.None)
        {
            OnSimpleType(type);
            if (IsContainedInSharedAssemblies(type))
            {
                OnByReferenceMarshalling(refKind);
            }
            else
            {
                HasError = true;
                ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.ReturnType_CannotBeByReference, GetCurrentLocation(), type));
                OnIdentityMarshalling();
            }
            return;
        }
        OnType(type);
    }

    protected virtual void OnParameter(IParameterSymbol parameterSymbol)
    {
        if (parameterSymbol.RefKind != RefKind.None)
        {
            OnSimpleType(parameterSymbol.Type);
            if (IsContainedInSharedAssemblies(parameterSymbol.Type))
            {
                OnByReferenceMarshalling(parameterSymbol.RefKind);
            }
            else
            {
                HasError = true;
                ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Parameter_CannotBeByReference, GetCurrentLocation(), parameterSymbol.Type));
                OnIdentityMarshalling();
            }
            return;
        }
        OnType(parameterSymbol.Type);
    }

    private void OnType(ITypeSymbol type)
    {
        CancellationToken.ThrowIfCancellationRequested();
        OnSimpleType(type);
        if (type is IPointerTypeSymbol or IFunctionPointerTypeSymbol)
        {
            OnSimpleCastMarshalling("nint");
            return;
        }
        if (IsContainedInSharedAssemblies(type))
        {
            OnIdentityMarshalling();
            return;
        }
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Interface } intf)
        {
            if (IsGenericCollectionType(intf, out CollectionType collectionType))
            {
                OnGenericCollectionMarshalling(collectionType, intf.TypeArguments[0]);
                return;
            }
            OnComSharpInterfaceMarshalling();
            return;
        }
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } ut })
        {
            if (GetSpecialTypeKeyword(ut.SpecialType) is { } comSharpType)
            {
                OnSimpleCastMarshalling(comSharpType);
            }
            else
            {
                OnErrorMarshalling(type);
            }
            return;
        }
        if (type is INamedTypeSymbol t && IsTuple(t, out bool isValueTuple, out ImmutableArray<ITypeSymbol> elementTypes))
        {
            OnTupleMarshalling(isValueTuple, elementTypes);
            return;
        }
        OnErrorMarshalling(type);
    }

    protected virtual void OnSimpleType(ITypeSymbol typeName) { }

    protected virtual void DefaultOnMarshalling() { }

    private void OnErrorMarshalling(ITypeSymbol type)
    {
        HasError = true;
        ReportDiagnostic?.Invoke(Diagnostic.Create(DiagnosticDescriptors.Parameter_UnsupportedType, GetCurrentLocation(), type));
        OnIdentityMarshalling();
    }

    protected virtual void OnIdentityMarshalling() => DefaultOnMarshalling();

    protected virtual void OnSimpleCastMarshalling(string comSharpTypeName) => DefaultOnMarshalling();

    protected virtual void OnComSharpInterfaceMarshalling() => DefaultOnMarshalling();

    protected virtual void OnTupleMarshalling(bool isValueTuple, ImmutableArray<ITypeSymbol> elementTypes)
    {
        foreach (var element in elementTypes)
        {
            OnType(element);
        }
        DefaultOnMarshalling();
    }

    protected virtual void OnByReferenceMarshalling(RefKind refKind) => DefaultOnMarshalling();

    protected virtual void OnGenericCollectionMarshalling(CollectionType collectionType, ITypeSymbol elementType)
    {
        OnType(elementType);
        DefaultOnMarshalling();
    }

    private bool IsContainedInSharedAssemblies(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol arraySymbol => IsContainedInSharedAssemblies(arraySymbol.ElementType),
        IPointerTypeSymbol pointerSymbol => IsContainedInSharedAssemblies(pointerSymbol.PointedAtType),
        IFunctionPointerTypeSymbol { Signature: var signature } =>
            IsContainedInSharedAssemblies(signature.ReturnType)
            && signature.Parameters.All(p => IsContainedInSharedAssemblies(p.Type)),
        INamedTypeSymbol { IsGenericType: true } namedTypeSymbol =>
            !SymbolEqualityComparer.Default.Equals(namedTypeSymbol.ContainingAssembly, _userAssembly)
            && namedTypeSymbol.TypeArguments.All(IsContainedInSharedAssemblies),
        INamedTypeSymbol namedTypeSymbol =>
            !SymbolEqualityComparer.Default.Equals(namedTypeSymbol.ContainingAssembly, _userAssembly),
        _ => true,
    };

    private static Guid? GetGuidAttributeValue(INamedTypeSymbol guidAttributeSymbol, INamedTypeSymbol type)
    {
        var guidAttribute = type.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, guidAttributeSymbol));
        if (guidAttribute is { ConstructorArguments: [{ Value: string guidString }] } && Guid.TryParse(guidString, out var guid))
        {
            return guid;
        }
        return null;
    }

    private static string? GetSpecialTypeKeyword(SpecialType specialType) => specialType switch
    {
        SpecialType.System_Boolean => "bool",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Char => "char",
        SpecialType.System_Decimal => "decimal",
        SpecialType.System_Double => "double",
        SpecialType.System_Int16 => "short",
        SpecialType.System_Int32 => "int",
        SpecialType.System_Int64 => "long",
        SpecialType.System_IntPtr => "nint",
        SpecialType.System_Object => "object",
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Single => "float",
        SpecialType.System_String => "string",
        SpecialType.System_UInt16 => "ushort",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_UInt64 => "ulong",
        SpecialType.System_UIntPtr => "nuint",
        SpecialType.System_Void => "void",
        _ => null
    };

    private static bool IsGenericCollectionType(INamedTypeSymbol type, out CollectionType collectionType)
    {
        collectionType = default;
        if (type.Arity != 1)
        {
            return false;
        }
        switch (type.OriginalDefinition.SpecialType)
        {
            case SpecialType.System_Collections_Generic_IEnumerable_T:
                collectionType = CollectionType.IEnumerable;
                return true;
            case SpecialType.System_Collections_Generic_IEnumerator_T:
                collectionType = CollectionType.IEnumerator;
                return true;
            case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
                collectionType = CollectionType.IReadOnlyCollection;
                return true;
            case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                collectionType = CollectionType.IReadOnlyList;
                return true;
            case SpecialType.System_Collections_Generic_ICollection_T:
                collectionType = CollectionType.ICollection;
                return true;
            case SpecialType.System_Collections_Generic_IList_T:
                collectionType = CollectionType.IList;
                return true;
        }
        switch (type)
        {
            case { ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } }:
                switch (type.Name)
                {
                    case "IAsyncEnumerable":
                        collectionType = CollectionType.IAsyncEnumerable;
                        return true;
                    case "IAsyncEnumerator":
                        collectionType = CollectionType.IAsyncEnumerator;
                        return true;
                }
                break;
        }
        return false;
    }

    private static bool IsTuple(INamedTypeSymbol type, out bool isValueTuple, out ImmutableArray<ITypeSymbol> elements)
    {
        isValueTuple = false;
        elements = default;
        if (type.IsTupleType)
        {
            isValueTuple = true;
            elements = type.TupleElements.Select(x => x.Type).ToImmutableArray();
            return true;
        }
        if (type is { Name: nameof(Tuple), ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
        {
            isValueTuple = false;
            elements = type.GetMembers()
                .Where(m => m is IPropertySymbol { ExplicitInterfaceImplementations: [], DeclaredAccessibility: Accessibility.Public })
                .OrderBy(m => m.Name)
                .Select(x => ((IPropertySymbol)x).Type)
                .ToImmutableArray();
            return true;
        }
        return false;
    }
}
