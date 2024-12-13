// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Grammars.GoldParser;
using System.Collections.Immutable;

namespace Farkle.Tests.CSharp;

internal class GrammarTests
{
    [TestCase("legacy.cgt", GrammarFileType.GoldParser)]
    [TestCase("JSON.egt", GrammarFileType.GoldParser)]
    [TestCase("JSON.egtn", GrammarFileType.EgtNeo)]
    public void TestInvalidFiles(string fileName, GrammarFileType fileType)
    {
        var buffer = File.ReadAllBytes(TestUtilities.GetResourceFile(fileName));
        var header = GrammarHeader.Read(buffer);
        Assert.Multiple(() =>
        {
            Assert.That(header.IsSupported, Is.False);
            Assert.That(header.FileType, Is.EqualTo(fileType));
        });
    }

    [TestCaseSource(typeof(TestUtilities), nameof(TestUtilities.GoldParserGrammars))]
    public void TestGoldParserConversion(string goldGrammar)
    {
        var convertedGrammar = ConvertGrammarFile(goldGrammar);
        _ = Grammar.Load(convertedGrammar);

        var farkleGrammar = Path.ChangeExtension(goldGrammar, ".grammar.dat");
        if (File.Exists(farkleGrammar))
        {
            var originalFarkleGrammar = File.ReadAllBytes(farkleGrammar);
            Assert.That(convertedGrammar, Is.EqualTo(originalFarkleGrammar));
        }

        static ImmutableArray<byte> ConvertGrammarFile(string path)
        {
            using var stream = File.OpenRead(path);
            return GoldGrammarConverter.Convert(GoldGrammarReader.ReadGrammar(stream));
        }
    }

    [TestCaseSource(typeof(TestUtilities), nameof(TestUtilities.Farkle7Grammars))]
    public void TestReadGrammar(string grammarFile)
    {
        var filePath = TestUtilities.GetResourceFile(grammarFile);

        var grammar = Grammar.Load(filePath);

        Assert.Multiple(() =>
        {
            Assert.That(grammar.HasUnknownData, Is.False);
            Assert.That(grammar.GrammarInfo.Name, Is.Not.Empty);
            Assert.That(() => grammar.GrammarInfo.Attributes, Throws.Nothing);
            Assert.That(grammar.GrammarInfo.StartSymbol.Handle.HasValue);

            foreach (var tokenSymbol in grammar.TokenSymbols)
            {
                Assert.That(tokenSymbol.Name, Is.Not.Empty);
                Assert.That(() => tokenSymbol.Attributes, Throws.Nothing);
            }

            foreach (var nonterminal in grammar.Nonterminals)
            {
                Assert.That(nonterminal.Name, Is.Not.Empty);
                Assert.That(() => nonterminal.Attributes, Throws.Nothing);
            }

            foreach (var group in grammar.Groups)
            {
                Assert.That(group.Name, Is.Not.Empty);
                Assert.That(group.Container.Handle.HasValue);
                Assert.That(() => group.Attributes, Throws.Nothing);
                Assert.That(group.Start.Handle.HasValue);
                Assert.That(group.End.Handle.HasValue);
                Assert.That(group.Nesting.Count(), Is.EqualTo(group.Nesting.Count));
            }

            foreach (var production in grammar.Productions)
            {
                Assert.That(production.Head.Handle.HasValue);
                Assert.That(production.Members.Count(), Is.EqualTo(production.Members.Count));
            }

            if (grammar.DfaOnChar is { } dfa)
            {
                int count = 0;
                foreach (var state in dfa)
                {
                    Assert.That(state.StateIndex, Is.EqualTo(count));
                    Assert.That(() => state.DefaultTransition, Throws.Nothing);
                    Assert.That(state.Edges.Count(), Is.EqualTo(state.Edges.Count));
                    Assert.That(state.AcceptSymbols.Count(), Is.EqualTo(state.AcceptSymbols.Count));
                    count++;
                }
                Assert.That(count, Is.EqualTo(dfa.Count));
            }

            if (grammar.LrStateMachine is { } lr)
            {
                int count = 0;
                foreach (var state in lr)
                {
                    Assert.That(state.StateIndex, Is.EqualTo(count));
                    Assert.That(state.Actions.Count(), Is.EqualTo(state.Actions.Count));
                    Assert.That(state.EndOfFileActions.Count(), Is.EqualTo(state.EndOfFileActions.Count));
                    Assert.That(state.Gotos.Count(), Is.EqualTo(state.Gotos.Count));
                    count++;
                }
                Assert.That(count, Is.EqualTo(lr.Count));
            }
        });
    }

    [TestCase("gml.grammar.dat")] // Only test grammar with more than one group
    public void TestGrammarObjectEquality(string grammarFile)
    {
        var grammar1 = Grammar.Load(TestUtilities.GetResourceFile(grammarFile));
        var grammar2 = Grammar.Load(TestUtilities.GetResourceFile(grammarFile));

        var term1 = grammar1.Terminals.First();
        var term2 = grammar1.Terminals.ElementAt(1);
        var term3 = grammar2.Terminals.First();

        var nont1 = grammar1.Nonterminals.First();
        var nont2 = grammar1.Nonterminals.ElementAt(1);
        var nont3 = grammar2.Nonterminals.First();

        var group1 = grammar1.Groups.First();
        var group2 = grammar1.Groups.ElementAt(1);
        var group3 = grammar2.Groups.First();

        var prod1 = grammar1.Productions.First();
        var prod2 = grammar1.Productions.ElementAt(1);
        var prod3 = grammar2.Productions.First();

#pragma warning disable CS1718 // Comparison made to same variable
#pragma warning disable NUnit2010 // Use EqualConstraint for better assertion messages in case of failure
        Assert.Multiple(() =>
        {
            // Same object from same grammar is equal
            Assert.That(term1 == term1);
            // Different objects from same grammar are not equal
            Assert.That(term1 != term2);
            // Same objects from different grammars are not equal
            Assert.That(term1 != term3);

            Assert.That(nont1 == nont1);
            Assert.That(nont1 != nont2);
            Assert.That(nont1 != nont3);

            Assert.That(group1 == group1);
            Assert.That(group1 != group2);
            Assert.That(group1 != group3);

            Assert.That(prod1 == prod1);
            Assert.That(prod1 != prod2);
            Assert.That(prod1 != prod3);
        });
#pragma warning restore NUnit2010 // Use EqualConstraint for better assertion messages in case of failure
#pragma warning restore CS1718 // Comparison made to same variable
    }
}
