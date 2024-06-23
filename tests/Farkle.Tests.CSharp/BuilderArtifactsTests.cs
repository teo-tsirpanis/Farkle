// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;

using static Farkle.Builder.BuilderArtifacts;

namespace Farkle.Tests.CSharp;

internal class BuilderArtifactsTests
{
    [TestCase(None, None)]
    [TestCase(GrammarSummary, GrammarSummary)]
    [TestCase(GrammarLrStateMachine, GrammarSummary)]
    [TestCase(GrammarDfaOnChar, GrammarSummary)]
    [TestCase(TokenizerOnChar, GrammarDfaOnChar | GrammarSummary)]
    [TestCase(SemanticProviderOnChar, None)]
    [TestCase(BuilderArtifacts.CharParser, SemanticProviderOnChar | TokenizerOnChar | GrammarDfaOnChar | GrammarLrStateMachine | GrammarSummary)]
    public void TestBuildArtifacts(BuilderArtifacts requestedArtifacts, BuilderArtifacts builtArtifacts)
    {
        var result = Terminals.Int32("Number").Build(requestedArtifacts);

        // It is obvious that the requested artifacts will get built;
        // this way we don't have to specify them twice in the test cases.
        builtArtifacts |= requestedArtifacts;

        Assert.Multiple(() =>
        {
            AssertNullIf(result.Grammar, GrammarSummary);
            AssertNullIf(result?.Grammar?.LrStateMachine, GrammarLrStateMachine);
            AssertNullIf(result?.Grammar?.DfaOnChar, GrammarDfaOnChar);
            AssertNullIf(result?.TokenizerOnChar, TokenizerOnChar);
            AssertNullIf(result?.SemanticProviderOnChar, SemanticProviderOnChar);
            AssertNullIf(result?.CharParser, BuilderArtifacts.CharParser);
        });

        void AssertNullIf(object? obj, BuilderArtifacts artifact)
        {
            bool hasArtifact = (builtArtifacts & artifact) != 0;
            Assert.That(obj, hasArtifact ? Is.Not.Null : Is.Null);
        }
    }
}
