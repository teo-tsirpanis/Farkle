// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;

using static Farkle.Builder.BuilderOutputs;

namespace Farkle.Tests.CSharp;

internal class BuilderOutputsTests
{
    [TestCase(None, None)]
    [TestCase(GrammarSummary, GrammarSummary)]
    [TestCase(GrammarLrStateMachine, GrammarSummary)]
    [TestCase(GrammarDfaOnChar, GrammarSummary)]
    [TestCase(TokenizerOnChar, GrammarDfaOnChar | GrammarSummary)]
    [TestCase(SemanticProviderOnChar, None)]
    [TestCase(BuilderOutputs.CharParser, SemanticProviderOnChar | TokenizerOnChar | GrammarDfaOnChar | GrammarLrStateMachine | GrammarSummary)]
    public void TestBuildOutputs(BuilderOutputs requestedOutputs, BuilderOutputs builtOutputs)
    {
        var result = Terminals.Int32("Number").Build(requestedOutputs);

        // It is obvious that the requested outputs will get built;
        // this way we don't have to specify them twice in the test cases.
        builtOutputs |= requestedOutputs;

        using (Assert.EnterMultipleScope())
        {
            AssertNullIf(result.Grammar, GrammarSummary);
            AssertNullIf(result?.Grammar?.LrStateMachine, GrammarLrStateMachine);
            AssertNullIf(result?.Grammar?.DfaOnChar, GrammarDfaOnChar);
            AssertNullIf(result?.TokenizerOnChar, TokenizerOnChar);
            AssertNullIf(result?.SemanticProviderOnChar, SemanticProviderOnChar);
            AssertNullIf(result?.CharParser, BuilderOutputs.CharParser);
        }

        void AssertNullIf(object? obj, BuilderOutputs output)
        {
            bool hasOutput = (builtOutputs & output) != 0;
            Assert.That(obj, hasOutput ? Is.Not.Null : Is.Null);
        }
    }
}
