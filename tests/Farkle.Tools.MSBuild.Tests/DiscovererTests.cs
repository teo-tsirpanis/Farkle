// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Farkle.Tools.MSBuild.Tests
{
    public class DiscovererTests
    {
        [Fact]
        public void Discovered_Grammar_Count_Is_Correct()
        {
            var expected = PrecompilableGrammars.All.Length;
            var precompiledGrammarCount = PrecompiledGrammar.GetAllFromAssembly(Assembly.GetExecutingAssembly()).Count;
            Assert.Equal(expected, precompiledGrammarCount);
        }

        [Theory]
        [MemberData(nameof(PrecompilerTestData))]
        public void Individual_Discovered_Grammars_Are_Correct(PrecompilableDesigntimeFarkle pcdf)
        {
            var precompiledGrammar = pcdf.TryGetPrecompiledGrammar();
            Assert.NotNull(precompiledGrammar);
            var grammarName = precompiledGrammar!.Value.GrammarName;
            Assert.Equal(pcdf.Name, grammarName);

            var actualGrammar = precompiledGrammar.Value.GetGrammar();
            Assert.Equal(pcdf.Name, actualGrammar.Properties.Name);
            Assert.Equal(Grammars.GrammarSource.Precompiled, actualGrammar.Properties.Source);
            HtmlChecker.Check(actualGrammar);
        }

        public static IEnumerable<object[]> PrecompilerTestData =>
            PrecompilableGrammars.All.Select(x => new object[] { x });
    }
}
