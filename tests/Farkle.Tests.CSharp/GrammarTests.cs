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

    private static IEnumerable<string[]> FarkleAndGoldGrammars
    {
        get
        {
            foreach (string path in TestUtilities.Farkle7Grammars)
            {
                var egtFile = path.Replace(".grammar.dat", ".egt");
                if (File.Exists(egtFile))
                {
                    yield return new string[] { path, egtFile };
                }
                var cgtFile = path.Replace(".grammar.dat", ".cgt");
                if (File.Exists(egtFile))
                {
                    yield return new string[] { path, cgtFile };
                }
            }
        }
    }

    [TestCaseSource(nameof(FarkleAndGoldGrammars))]
    public void TestGoldParserConversion(string farkleGrammar, string goldGrammar)
    {
        var originalFarkleGrammar = File.ReadAllBytes(farkleGrammar);
        var convertedGrammar = ConvertGrammarFile(goldGrammar);

        Assert.That(convertedGrammar, Is.EqualTo(originalFarkleGrammar));

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
        var buffer = File.ReadAllBytes(filePath);

        var grammar = Grammar.Create(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(grammar.HasUnknownData, Is.False);
            Assert.That(grammar.GrammarInfo.Name.IsEmpty, Is.False);
            Assert.That(() => grammar.GrammarInfo.Attributes, Throws.Nothing);
            Assert.That(grammar.GrammarInfo.StartSymbol.HasValue);

            foreach (var tokenSymbol in grammar.TokenSymbols)
            {
                Assert.That(tokenSymbol.Name.IsEmpty, Is.False);
                Assert.That(() => tokenSymbol.Attributes, Throws.Nothing);
            }

            foreach (var nonterminal in grammar.Nonterminals)
            {
                Assert.That(nonterminal.Name.IsEmpty, Is.False);
                Assert.That(() => nonterminal.Attributes, Throws.Nothing);
            }

            foreach (var group in grammar.Groups)
            {
                Assert.That(group.Name.IsEmpty, Is.False);
                Assert.That(group.Container.HasValue);
                Assert.That(() => group.Attributes, Throws.Nothing);
                Assert.That(group.Start.HasValue);
                Assert.That(group.End.HasValue);
                Assert.That(group.Nesting.Count(), Is.EqualTo(group.Nesting.Count));
            }

            foreach (var production in grammar.Productions)
            {
                Assert.That(production.Head.HasValue);
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
}
