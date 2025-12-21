// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Build.Utilities;

namespace Farkle.Tools.Precompiler;

internal sealed class PrecompilerLoadContext : AssemblyLoadContext
{
    private readonly TaskLoggingHelper? _log;

    private readonly IReadOnlyDictionary<string, string> _references;

    public PrecompilerLoadContext(IReadOnlyDictionary<string, string> references, TaskLoggingHelper? log) :
        base($"Farkle.Tools.Precompiler.{nameof(PrecompilerLoadContext)}", isCollectible: true)
    {
        _references = references;
        _log = log;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        switch (assemblyName.Name)
        {
            case "mscorlib" or "System.Private.CoreLib" or "System.Runtime" or "netstandard": return null;
            case "System.Collections.Immutable": return typeof(ImmutableArray<>).Assembly;
        }
        if (_references.TryGetValue(assemblyName.FullName, out string? path))
        {
            _log?.LogMessage("Loading assembly from '{0}'", path);
            return LoadFromAssemblyPath(path);
        }
        return base.Load(assemblyName);
    }

    public Assembly LoadFromAssemblyPathInMemory(string path)
    {
        var symbolsPath = Path.ChangeExtension(path, ".pdb");
        using var assemblyFile = File.OpenRead(path);
        using var symbolsFile = LoadSymbols(symbolsPath);
        return LoadFromStream(assemblyFile, symbolsFile);

        Stream LoadSymbols(string path)
        {
            try
            {
                _log?.LogMessage("Reading symbols from '{0}'", path);
                return File.OpenRead(path);
            }
            catch (FileNotFoundException)
            {
                _log?.LogMessage("Symbols not found");
            }
            catch (Exception e)
            {
                _log?.LogMessage("Loading symbols failed: {0}", e);
            }
            return Stream.Null;
        }
    }
}
