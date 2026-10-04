// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public static class Extensions
{
    extension(INamedTypeSymbol type)
    {
        /// <summary>
        /// Returns the fully qualified metadata name of an <see cref="INamedTypeSymbol"/>.
        /// The returned value can be passed to <see cref="Compilation.GetTypeByMetadataName"/>.
        /// </summary>
        public string ToFullyQualifiedMetadataName()
        {
            string typeName;
            switch (type.ContainingType)
            {
                case null:
                    typeName = type.MetadataName;
                    break;
                default:
                    var types = new List<string>();
                    var current = type;
                    while (current is not null)
                    {
                        types.Add(current.MetadataName);
                        current = current.ContainingType;
                    }
                    types.Reverse();
                    typeName = string.Join("+", types);
                    break;
            }
            var ns = type.ContainingNamespace;
            if (ns is null or { IsGlobalNamespace: true })
            {
                return typeName;
            }
            return $"{ns.ToDisplayString()}.{typeName}";
        }
    }
}
