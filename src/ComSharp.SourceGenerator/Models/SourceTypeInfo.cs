// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace ComSharp;

/// <summary>
/// Contains information to write a <see langword="partial"/> type in source code.
/// </summary>
public sealed class SourceTypeInfo : IComparable<SourceTypeInfo>
{
    private readonly EquatableArray<ContainerInfo> _containers;

    private readonly string _sortName;

    public string Name => _containers[^1].Name;

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

    private SourceTypeInfo(EquatableArray<ContainerInfo> containers, string sortName)
    {
        _containers = containers;
        _sortName = sortName;
    }

    public static SourceTypeInfo? Create(INamedTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }
        var builder = ImmutableArray.CreateBuilder<ContainerInfo>();
        if (symbol.ContainingNamespace is INamespaceSymbol ns && !ns.IsGlobalNamespace)
        {
            builder.Add(new NamespaceInfo(ns.ToDisplayString()));
        }
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            if (!AddImpl(typeSymbol))
            {
                return null;
            }
        }
        if (builder.Count == 0)
        {
            return null;
        }
        var sortName = symbol.ToFullyQualifiedMetadataName();
        return new(builder.ToEquatableArray(), sortName);

        bool AddImpl(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingType is { } parent)
            {
                if (!AddImpl(parent))
                {
                    return false;
                }
            }
            var keywords = GetSymbolKeywords(typeSymbol);
            if (keywords is null)
            {
                return false;
            }
            var typeParameters = typeSymbol.TypeParameters.Select(x => x.Name).ToEquatableArray();
            builder.Add(new TypeInfo(typeSymbol.Name, null, keywords, typeParameters));
            return true;
        }
    }

    /// <summary>
    /// Begins writing a partial definition of the type.
    /// This method opens indentation blocks for the namespace and any nested types, if each exists.
    /// </summary>
    public void OpenWriteTo(IndentedTextWriter writer)
    {
        foreach (var x in _containers)
        {
            switch (x)
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
    }

    /// <summary>
    /// Closes the indentation blocks opened by <see cref="OpenWriteTo"/>.
    /// </summary>
    public void CloseWriteTo(IndentedTextWriter writer)
    {
        for (int i = 0; i < _containers.Count; i++)
        {
            writer.Indent--;
            writer.WriteLine('}');
        }
    }

    public override bool Equals(object? obj) => obj is SourceTypeInfo other && _containers.Equals(other._containers);

    public override int GetHashCode() => _containers.GetHashCode();

    public int CompareTo(SourceTypeInfo? other) => string.Compare(_sortName, other?._sortName, StringComparison.Ordinal);

    private abstract record ContainerInfo(string Name);

    private sealed record NamespaceInfo(string Name) : ContainerInfo(Name);

    private sealed record TypeInfo(string Name, SourceTypeInfo? Parent, string Keywords, EquatableArray<string> TypeParameters) : ContainerInfo(Name);
}
