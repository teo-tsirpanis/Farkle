// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using ComSharp;
using Microsoft.Build.Utilities;

namespace Farkle.Tools.Precompiler;

/// <summary>
/// Contains logic to load an assembly into an isolated context, find
/// an implementation of the precompiler interface, and invoke it.
/// </summary>
public sealed class PrecompilerHost
{
    private PrecompilerOptions Options { get; }

    private TaskLoggingHelper? Log => Options.Logger;

    private PrecompilerHost(PrecompilerOptions options)
    {
        Options = options;
    }

    private void EnsureGarbageCollected(WeakReference weakReference)
    {
        const int NumberOfTries = 10;
        int n = NumberOfTries;
        while (n > 0 && weakReference.IsAlive)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            n--;
        }
        if (!weakReference.IsAlive)
        {
            Log?.LogMessage("Assembly unloaded after {0} garbage collections.", NumberOfTries - n);
        }
        else
        {
            Log?.FailedToUnloadAssembly();
        }
    }

    private Assembly LoadFarkleAssembly(AssemblyLoadContext alc, Assembly userAssembly)
    {
        var farkleAssemblyName = Array.Find(userAssembly.GetReferencedAssemblies(), x => x.Name is nameof(Farkle));
        if (farkleAssemblyName is not null)
        {
            return alc.LoadFromAssemblyName(farkleAssemblyName);
        }
        else
        {
            Log?.LogMessage("No reference to Farkle assembly found; using the input assembly for precompilation.");
            return userAssembly;
        }
    }

    private IPrecompilerInterface? CreatePrecompilerInterface(Assembly farkleAssembly, bool isEmbeddedFarkleAssembly)
    {
        const string PrecompilerInterfaceTypeName = "Farkle.Runtime.PrecompilerEntryPoints";
        const string PrecompilerInterfaceFactoryName = "GetPrecompilerInterface";

        var precompilerInterfaceFactory =
            farkleAssembly
                .GetType(PrecompilerInterfaceTypeName)
                ?.GetMethod(PrecompilerInterfaceFactoryName, BindingFlags.Static | BindingFlags.NonPublic);
        if (precompilerInterfaceFactory is null)
        {
            if (isEmbeddedFarkleAssembly)
            {
                Log?.LogMessage("No precompiler interface found in embedded Farkle assembly, skipping.");
            }
            else
            {
                // The external Farkle assembly should always have the precompiler interface factory.
                Log?.IncompatiblePrecompilerInterface();
            }
            return null;
        }
        if (precompilerInterfaceFactory.Invoke(null, null) is not ComSharpObject intfComSharp
            || !PrecompilerInterfaceWrappers.Instance.ConvertToDotNet(intfComSharp).IsComSharp(out IPrecompilerInterface? intf))
        {
            Log?.IncompatiblePrecompilerInterface();
            return null;
        }
        return intf;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private List<PrecompiledGrammar> PrecompileAssemblyFromPathImpl(string assemblyPath, out WeakReference alcWeakRef)
    {
        var alc = new PrecompilerLoadContext(Options.AssemblyReferences, Log);
        alcWeakRef = new(alc);
        try
        {
            var userAssembly = alc.LoadFromAssemblyPathInMemory(assemblyPath);
            var farkleAssembly = LoadFarkleAssembly(alc, userAssembly);
            var precompilerInterface = CreatePrecompilerInterface(farkleAssembly, isEmbeddedFarkleAssembly: userAssembly == farkleAssembly);

            List<PrecompiledGrammar> grammars = [];
            if (precompilerInterface is not null)
            {
                bool doEmitReport = Options.ConflictReportMode != ConflictReportMode.ErrorsOnly;
                bool doEmitConflictErrors = Options.ConflictReportMode != ConflictReportMode.ReportOnly;
                var conflictTracker = Log is null ? null : new LrConflictTracker(new ComSharpLoggerAdapter(Log), doEmitConflictErrors);

                var options = new ComSharpPrecompilerOptions(conflictTracker, Options.CancellationToken);

                foreach (var x in precompilerInterface.DiscoverAndPrecompile(userAssembly, options))
                {
                    var grammar = new PrecompiledGrammar(x);
                    grammars.Add(grammar);

                    if (doEmitReport && conflictTracker?.ConflictCount is { } conflictCount && conflictCount != 0)
                    {
                        if (!doEmitConflictErrors)
                        {
                            Log?.LrConflicts(conflictCount);
                        }
                        Options.GrammarConflict(grammar.GrammarFile);
                    }

                    // This is not very clean; it assumes that between each iteration only one grammar gets precompiled.
                    // Should we add an explicit Build() method on IPrecompiledGrammar instead?
                    conflictTracker?.Reset();
                }
            }
            return grammars;
        }
        finally
        {
            alc.Unload();
        }
    }

    public static List<PrecompiledGrammar> PrecompileAssemblyFromPath(string assemblyPath, PrecompilerOptions options)
    {
        var host = new PrecompilerHost(options);
        var result = host.PrecompileAssemblyFromPathImpl(assemblyPath, out var alcWeakRef);
        host.EnsureGarbageCollected(alcWeakRef);
        return result;
    }
}
