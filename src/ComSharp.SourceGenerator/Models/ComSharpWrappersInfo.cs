// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public sealed record InterfaceWrappersInfo(string DisplayName, string MetadataName, ComSharpInterfaceOptionsSpec Options)
{
    public static InterfaceWrappersInfo Create(INamedTypeSymbol symbol, ComSharpInterfaceOptionsSpec options)
    {
        return new(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), symbol.ToFullyQualifiedMetadataName(), options);
    }
}

public sealed record ComSharpWrappersInfo(TypeOrNamespaceInfo Type, EquatableArray<InterfaceWrappersInfo> Interfaces);
