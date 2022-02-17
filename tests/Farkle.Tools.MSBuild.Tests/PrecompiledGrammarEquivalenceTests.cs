// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Farkle.Builder;
using Farkle.Grammars;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Farkle.Tools.MSBuild.Tests
{
    public class PrecompiledGrammarEquivalenceTests
    {
        /// <summary>
        /// Asserts that two <see cref="Farkle.Grammars.Grammar"/>s have the exact same structure.
        /// </summary>
        /// <remarks>
        /// This is different from the grammar equivalence test in the F# tests. Here, if
        /// two grammars have two states swapped by each other, the grammars would not be
        /// considered equivalent.
        /// </remarks>
        private static void AssertStrictEquivalence(Grammars.Grammar expected, Grammars.Grammar actual)
        {
            static void AssertEqualSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) =>
                Assert.Equal(expected, actual);

            Assert.Equal(expected.StartSymbol, actual.StartSymbol);
            AssertEqualSequence(expected.Symbols.Terminals, actual.Symbols.Terminals);
            AssertEqualSequence(expected.Symbols.Nonterminals, actual.Symbols.Nonterminals);
            AssertEqualSequence(expected.Symbols.NoiseSymbols, actual.Symbols.NoiseSymbols);
            AssertEqualSequence(expected.Productions, actual.Productions);
            AssertEqualSequence(expected.Groups, actual.Groups);
            AssertEqualSequence(expected.LALRStates, actual.LALRStates);
            AssertEqualSequence(expected.DFAStates, actual.DFAStates);
        }

        [Theory]
        [InlineData(nameof(PrecompilableGrammars.PublicJSON))]
        [InlineData(nameof(PrecompilableGrammars.InternalRegex))]
        public void Check_Precompiled_Grammar_Equivalence(string fieldName)
        {
            var pcdf =
                typeof(PrecompilableGrammars)
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null)
                as PrecompilableDesigntimeFarkle;
            if (pcdf == null)
                throw new MissingFieldException(typeof(PrecompilableDesigntimeFarkle).FullName, fieldName);

            var precompiledGrammar =
                pcdf.TryGetPrecompiledGrammar()?.Value.GetGrammar() ?? throw new NotNullException();
            var builtGrammar = pcdf.InnerDesigntimeFarkle.BuildUntyped().GetGrammar();

            Assert.Equal(GrammarSource.Precompiled, precompiledGrammar.Properties.Source);
            Assert.Equal(GrammarSource.Built, builtGrammar.Properties.Source);
            AssertStrictEquivalence(builtGrammar, precompiledGrammar);
        }
    }
}
