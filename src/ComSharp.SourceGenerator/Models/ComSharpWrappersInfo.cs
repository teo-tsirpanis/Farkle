// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace ComSharp;

public sealed record InterfaceWrappersInfo(string DisplayName, string MetadataName, ComSharpInterfaceOptionsSpec Options)
{
    public static InterfaceWrappersInfo Create(INamedTypeSymbol symbol, ComSharpInterfaceOptionsSpec options)
    {
        return new(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), symbol.ToFullyQualifiedMetadataName(), options);
    }
}

public sealed record ComSharpWrappersInfo(TypeOrNamespaceInfo Type, EquatableArray<InterfaceWrappersInfo> Interfaces);

public static partial class Extensions
{
    extension(IEnumerable<InterfaceWrappersInfo> interfaces)
    {
        /// <summary>
        /// Aggregates <see cref="InterfaceWrappersInfo"/> instances for the same interface type,
        /// combining their <see cref="InterfaceWrappersInfo.Options"/>.
        /// </summary>
        public InterfaceWrappersInfo Aggregate()
        {
            var iter = interfaces.GetEnumerator();
            if (!iter.MoveNext())
            {
                throw new InvalidOperationException("The collection is empty.");
            }
            var displayName = iter.Current.DisplayName;
            var metadataName = iter.Current.MetadataName;
            var options = iter.Current.Options;
            while (iter.MoveNext())
            {
                Debug.Assert(displayName == iter.Current.DisplayName);
                Debug.Assert(metadataName == iter.Current.MetadataName);
                options |= iter.Current.Options;
            }
            return new(displayName, metadataName, options);
        }
    }
}
