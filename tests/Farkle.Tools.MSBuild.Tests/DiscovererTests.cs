// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using PCDF = Farkle.Builder.PrecompilableDesigntimeFarkle<int>;

namespace Farkle.Tools.MSBuild.Tests
{
    public class DiscovererTests
    {
        private static PCDF CreatePCDF(string name) => Terminals.Int32(name).MarkForPrecompile();

        // The following designtime Farkles must be discovered.
        public static readonly PCDF Public = CreatePCDF(nameof(Public));
        internal static readonly PCDF Internal = CreatePCDF(nameof(Internal));
        private static readonly PCDF Private = CreatePCDF(nameof(Private));

        static class NestedClass
        {
            public static readonly PCDF Nested = CreatePCDF(nameof(Nested));
        }

        public static readonly PCDF MarkedAgain =
            Public.Rename(nameof(MarkedAgain)).MarkForPrecompile();

        public static readonly PrecompilableDesigntimeFarkle Untyped =
            Terminal.Literal(nameof(Untyped)).MarkForPrecompile();

        // And the following must not.
        public static readonly PCDF SameReference = Public;
        public static PCDF Mutable = CreatePCDF(nameof(Mutable));
        public readonly PCDF InstanceField = CreatePCDF(nameof(InstanceField));
        public PCDF InstanceProperty => CreatePCDF(nameof(InstanceProperty));
        public static PCDF StaticProperty => CreatePCDF(nameof(StaticProperty));
        public static readonly DesigntimeFarkle<int> Unmarked = CreatePCDF(nameof(Unmarked));

        public static readonly DesigntimeFarkle UnmarkedUntyped =
            Terminal.Literal(nameof(UnmarkedUntyped)).MarkForPrecompile();

        [Fact]
        public void TestDiscoverer()
        {
            var expected = new HashSet<PrecompilableDesigntimeFarkle>()
            {
                Public,
                Internal,
                Private,
                NestedClass.Nested,
                MarkedAgain,
                Untyped
            };
            var precompiledGrammarCount = PrecompiledGrammar.GetAllFromAssembly(Assembly.GetExecutingAssembly()).Count;
            Assert.Equal(expected.Count, precompiledGrammarCount);

            foreach (var x in expected)
            {
                var precompiledGrammar = x.TryGetPrecompiledGrammar();
                Assert.NotNull(precompiledGrammar);
                var grammarName = precompiledGrammar!.Value.GrammarName;
                Assert.Equal(x.Name, grammarName);

                var actualGrammar = precompiledGrammar.Value.GetGrammar();
                Assert.Equal(x.Name, actualGrammar.Properties.Name);
                Assert.Equal(Grammar.GrammarSource.Precompiled, actualGrammar.Properties.Source);
                DocumentationChecker.CheckDocumentation(actualGrammar);
            }
        }
    }
}
