// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Xunit;

namespace Farkle.Tools.MSBuild.Tests
{
    public class UnloadabilityTests
    {
        [Fact]
        public void Test()
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            static WeakReference DoTest()
            {
                var ctx = new AssemblyLoadContext(nameof(UnloadabilityTests), true);
                try
                {
                    var asm = ctx.LoadFromAssemblyPath(typeof(UnloadabilityTests).Assembly.Location);
                    Assert.NotSame(asm, typeof(UnloadabilityTests).Assembly);

                    var allPrecompiled =
                        asm.GetType(typeof(PrecompilableGrammars).FullName!, true)
                            ?.GetProperty(nameof(PrecompilableGrammars.All))
                            ?.GetValue(null) as HashSet<PrecompilableDesigntimeFarkle>;
                    // With Assert.NotNull the compiler would not
                    // shut up about allPrecompiled maybe being null.
                    if (allPrecompiled == null)
                        throw new ArgumentNullException(nameof(allPrecompiled));

                    Assert.Equal(PrecompilableGrammars.All.Count, allPrecompiled.Count);
                    Assert.All(allPrecompiled, pcdf => Assert.Same(asm, pcdf.Assembly));
                    Assert.Equal(PrecompilableGrammars.All.Count, PrecompiledGrammar.GetAllFromAssembly(asm).Count);
                    return new WeakReference(allPrecompiled.First());
                }
                finally
                {
                    ctx.Unload();
                }
            }

            var wr = DoTest();
            for (int i = 0; wr.IsAlive && i < 10; i++)
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
            }
            // Placing breakpoints before that final assert makes the tests
            // fail because the debugger will hold the weak reference's target.
            Assert.False(wr.IsAlive, "The assembly load context could not be unloaded");
        }
    }
}
