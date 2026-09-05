// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Analyzers.EnhancedSyntax;
using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;

namespace Farkle.Analyzers.Tests;

public class AnalyzerDependencyTests
{
    [Test]
    public async Task TestAnalyzersDontDependOnWorkspaces()
    {
        var alc = new BlockWorkspaceAssembliesLoadContext();
        try
        {
            // Load all types not in a namespace ending in ".Fixers", and observe that
            // they don't attempt to load any Roslyn Workspaces assemblies.
            var analyzersAssembly = alc.LoadFromAssemblyPath(typeof(ProductionBuilderFactoryAnalyzer).Assembly.Location);
            List<int> nonFixerTypeDefinitionTokens = GetNonFixerTypeDefinitionTokens(analyzersAssembly);
            foreach (var mdToken in nonFixerTypeDefinitionTokens)
            {
                var type = analyzersAssembly.ManifestModule.ResolveType(mdToken);
                if (type.IsGenericType)
                {
                    continue;
                }
                alc.TypeUnderExamination = type;
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (member is not MethodBase { IsAbstract: false, IsGenericMethodDefinition: false } method)
                    {
                        continue;
                    }
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
            }
        }
        finally
        {
            alc.TypeUnderExamination = null; // If we don't reset this, the ALC will not be able to be collected after we call Unload().
            alc.Unload();
        }

        // Gets the metadata tokens of all type definitions in the given assembly that are not in a namespace ending with ".Fixers".
        static List<int> GetNonFixerTypeDefinitionTokens(Assembly assembly)
        {
            MetadataReader mdReader;
            unsafe
            {
                if (!assembly.TryGetRawMetadata(out var blob, out var length))
                {
                    Assert.Inconclusive("Could not create a metadata reference for the Farkle.Analyzers assembly.");
                }
                mdReader = new MetadataReader(blob, length);
            }
            var result = new List<int>();
            foreach (var t in mdReader.TypeDefinitions)
            {
                if (MetadataTokens.GetRowNumber(t) == 1) // Skip <Module>
                {
                    continue;
                }
                var typeDef = mdReader.GetTypeDefinition(t);
                // Nested types don't have namespaces; find the topmost type and get its namespace instead.
                while (typeDef.GetDeclaringType() is { IsNil: false } declaringType)
                {
                    typeDef = mdReader.GetTypeDefinition(declaringType);
                }
                var @namespace = mdReader.GetString(typeDef.Namespace);
                if (@namespace.EndsWith(".Fixers", StringComparison.Ordinal))
                {
                    continue;
                }
                result.Add(0x02000000 | MetadataTokens.GetRowNumber(mdReader, t));
            }
            GC.KeepAlive(assembly);
            return result;
        }
    }

    /// <summary>
    /// An <see cref="AssemblyLoadContext"/> that blocks loading Roslyn Workspaces assemblies, while
    /// delegating everything else to the default context.
    /// </summary>
    private sealed class BlockWorkspaceAssembliesLoadContext() : AssemblyLoadContext(nameof(BlockWorkspaceAssembliesLoadContext), isCollectible: true)
    {
        public Type? TypeUnderExamination { get; set; }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string name = assemblyName.Name ?? "";
            if (name.StartsWith("Microsoft.CodeAnalysis.", StringComparison.Ordinal) &&
                name.EndsWith(".Workspaces", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Attempted to load {assemblyName} while performing reflection on {TypeUnderExamination?.FullName ?? "unknown type"}.");
            }
            return null;
        }
    }
}
