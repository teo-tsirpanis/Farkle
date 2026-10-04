// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ComSharp;

public sealed class GenerateComSharpWrappersSpec(ITypeSymbol type)
{
    public ITypeSymbol Type { get; } = type;

    public ComSharpInterfaceOptionsSpec Options { get; set; } = ComSharpInterfaceOptionsSpec.Both;

    public static bool TryParse(AttributeData attribute, [NotNullWhen(true)] out GenerateComSharpWrappersSpec? result)
    {
        result = null;
        if (attribute.AttributeClass is not { MetadataName: Constants.GenerateComSharpWrappersAttributeName })
        {
            return false;
        }
        if (attribute.ConstructorArguments is not [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol type }])
        {
            return false;
        }
        result = new(type);
        foreach (var namedArgument in attribute.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case nameof(Options) when namedArgument.Value is { Kind: TypedConstantKind.Enum, Value: int value }:
                    // Checking for the exact type of the enum seems like a stretch, and the compiler
                    // would raise an error either way.
                    result.Options = (ComSharpInterfaceOptionsSpec)value;
                    break;
                default:
                    return false;
            }
        }
        return true;
    }
}
