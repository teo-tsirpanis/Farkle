// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Farkle.Grammars;
using NUnit.Framework;

namespace Farkle.Tools.MSBuild.Tests;

public class UnloadabilityTests
{
    [Test]
    public void TestPrecompiledGrammarKeepsAlcAlive()
    {
        var grammar = TestGrammars.GrammarFactory();
        var grammarFromExternalAlc = LoadAssemblyAndGetGrammar(out var alcWeakRef);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grammar, Is.Not.SameAs(grammarFromExternalAlc));
            Assert.That(grammar.Data != grammarFromExternalAlc.Data);
            Assert.That(grammar.Data.SequenceEqual(grammarFromExternalAlc.Data));
        }

        int i;
        for (i = 0; i < 10 && alcWeakRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.That(alcWeakRef.IsAlive, $"ALC was unloaded after {i} iterations.");

        GC.KeepAlive(grammarFromExternalAlc);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Grammar LoadAssemblyAndGetGrammar(out WeakReference alcWeakRef)
        {
            var alc = new TestAssemblyLoadContext();
            alcWeakRef = new WeakReference(alc, trackResurrection: true);
            var asm = alc.LoadFromAssemblyPathInMemory(typeof(UnloadabilityTests).Assembly.Location);
            var type = asm.GetType(typeof(TestGrammars).FullName!, throwOnError: true)!;
            var method = type.GetMethod(nameof(TestGrammars.GrammarFactory))!;
            Assert.That(method, Is.Not.Null);
            var grammar = (Grammar)method.Invoke(null, null)!;
            alc.Unload();
            return grammar;
        }
    }

    private sealed class TestAssemblyLoadContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => assemblyName.Name switch
        {
            "Farkle" => typeof(Grammar).Assembly,
            _ => base.Load(assemblyName),
        };

        public Assembly LoadFromAssemblyPathInMemory(string path)
        {
            using var stream = File.OpenRead(path);
            return LoadFromStream(stream);
        }
    }
}
