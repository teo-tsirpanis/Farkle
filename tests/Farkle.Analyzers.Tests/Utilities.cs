// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;

namespace Farkle.Analyzers.Tests;

public static class Utilities
{
    extension(MetadataReference)
    {
        public static MetadataReference? TryCreateFromRawMetadata(Assembly assembly)
        {
            unsafe
            {
                if (!assembly.TryGetRawMetadata(out var blob, out var blobLength))
                {
                    return null;
                }
                var moduleMd = ModuleMetadata.CreateFromMetadata((IntPtr)blob, blobLength, () => GC.KeepAlive(assembly));
                var assemblyMd = AssemblyMetadata.Create(moduleMd);
                return assemblyMd.GetReference(filePath: assembly.Location, display: assembly.FullName);
            }
        }
    }
}
