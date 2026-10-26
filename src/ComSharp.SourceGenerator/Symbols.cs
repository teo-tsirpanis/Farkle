// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public sealed class Symbols
{
    public required INamedTypeSymbol ComSharpWrappers { get; init; }

    public required INamedTypeSymbol EmbeddedAttribute { get; init; }

    public required INamedTypeSymbol GuidAttribute { get; init; }

    public required INamedTypeSymbol GenerateComSharpWrappersAttribute { get; init; }

    public static Symbols? Create(Compilation compilation)
    {
        var comSharpWrappers = compilation.GetTypeByMetadataName(Constants.ComSharpWrappersName);
        var embeddedAttribute = compilation.GetTypeByMetadataName(Constants.EmbeddedAttributeName);
        var guidAttribute = compilation.GetTypeByMetadataName(Constants.GuidAttributeName);
        var generateComSharpWrappersAttribute = compilation.GetTypeByMetadataName(Constants.GenerateComSharpWrappersAttributeName);
        if (comSharpWrappers is null || embeddedAttribute is null || guidAttribute is null || generateComSharpWrappersAttribute is null)
        {
            return null;
        }
        return new Symbols
        {
            ComSharpWrappers = comSharpWrappers,
            EmbeddedAttribute = embeddedAttribute,
            GuidAttribute = guidAttribute,
            GenerateComSharpWrappersAttribute = generateComSharpWrappersAttribute,
        };
    }
}
