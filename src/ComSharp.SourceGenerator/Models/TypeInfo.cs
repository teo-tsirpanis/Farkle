// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

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
        _ when typeSymbol.IsUnion => "union",
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
            if (namespaceSymbol.IsGlobalNamespace)
            {
                return null;
            }
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
            return new TypeInfo(typeSymbol.Name, parent, keywords, typeParameters);
        }
        return ErrorTypeInfo.Instance;
    }

    public void OpenWriteTo(IndentedTextWriter writer)
    {
        switch (this)
        {
            case NamespaceInfo ns:
                writer.WriteLine($"namespace {ns.Name}");
                writer.WriteLine('{');
                writer.Indent++;
                break;
            case TypeInfo typeInfo:
                typeInfo.Parent?.OpenWriteTo(writer);
                writer.Write($"partial {typeInfo.Keywords} {typeInfo.Name}");
                if (typeInfo.TypeParameters.Count > 0)
                {
                    writer.Write('<');
                    for (int i = 0; i < typeInfo.TypeParameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            writer.Write(", ");
                        }
                        writer.Write(typeInfo.TypeParameters[i]);
                    }
                    writer.Write('>');
                }
                writer.WriteLine();
                writer.WriteLine('{');
                writer.Indent++;
                break;
        }
    }

    public void CloseWriteTo(IndentedTextWriter writer)
    {
        var x = this;
        while (x is not null)
        {
            writer.Indent--;
            writer.WriteLine('}');
            x = (x as TypeInfo)?.Parent;
        }
    }
}

public sealed record NamespaceInfo(string Name) : TypeOrNamespaceInfo(Name);

public sealed record TypeInfo(string Name, TypeOrNamespaceInfo? Parent, string Keywords, EquatableArray<string> TypeParameters) : TypeOrNamespaceInfo(Name);

public sealed record ErrorTypeInfo : TypeOrNamespaceInfo
{
    private ErrorTypeInfo() : base("<error>") { }

    public static ErrorTypeInfo Instance { get; } = new ErrorTypeInfo();
}
