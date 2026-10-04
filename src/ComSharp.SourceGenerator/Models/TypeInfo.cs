// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public abstract record TypeOrNamespaceInfo(string Name)
{
    private static string? GetSymbolKeywords(INamedTypeSymbol typeSymbol) => typeSymbol.TypeKind switch
    {
        TypeKind.Class when typeSymbol.IsRecord => "record",
        TypeKind.Class => "class",
        TypeKind.Struct when typeSymbol.IsRecord => "record struct",
        TypeKind.Struct => "struct",
        TypeKind.Interface => "interface",
        _ => null
    };

    public static TypeOrNamespaceInfo? Create(INamespaceOrTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }
        var parent = Create(symbol.ContainingSymbol as INamespaceOrTypeSymbol);
        if (parent is ErrorTypeInfo)
        {
            return parent;
        }
        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            return new NamespaceInfo(namespaceSymbol.ToDisplayString());
        }
        else if (symbol is INamedTypeSymbol typeSymbol)
        {
            var typeParameters = typeSymbol.TypeParameters.Select(x => x.Name).ToEquatableArray();
            var keywords = GetSymbolKeywords(typeSymbol);
            if (keywords is null)
            {
                return ErrorTypeInfo.Instance;
            }
            return new TypeInfo(typeSymbol.Name, parent, typeSymbol.TypeKind.ToString(), typeParameters);
        }
        return ErrorTypeInfo.Instance;
        throw new ArgumentException("Symbol must be a namespace or type.", nameof(symbol));
    }
}

public sealed record NamespaceInfo(string Name) : TypeOrNamespaceInfo(Name);

public sealed record TypeInfo(string Name, TypeOrNamespaceInfo? Parent, string Kind, EquatableArray<string> TypeParameters) : TypeOrNamespaceInfo(Name);

public sealed record ErrorTypeInfo : TypeOrNamespaceInfo
{
    private ErrorTypeInfo() : base("<error>") { }

    public static ErrorTypeInfo Instance { get; } = new ErrorTypeInfo();
}
