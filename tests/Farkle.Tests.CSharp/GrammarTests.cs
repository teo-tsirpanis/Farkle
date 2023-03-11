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
                foreach (var nesting in group.Nesting)
                {
                    Assert.That(nesting.Name.IsEmpty, Is.False);
                }
            }

            foreach (var production in grammar.Productions)
            {
                Assert.That(production.Head.HasValue);
                foreach (var member in production.Members)
                {
                    Assert.That(member.HasValue);
                }
            }
            if (grammar.DfaOnChar is { } dfa)
            {
                int count = 0;
                foreach (var state in dfa)
                {
                    Assert.That(state.StateIndex, Is.EqualTo(count));
                    Assert.That(() => state.DefaultTransition, Throws.Nothing);
                    Assert.That(() => { foreach (var edge in state.Edges) { } }, Throws.Nothing);
                    Assert.That(() => { foreach (var acceptSymbol in state.AcceptSymbols) { } }, Throws.Nothing);
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
                    Assert.That(() => { foreach (var action in state.Actions) { } }, Throws.Nothing);
                    Assert.That(() => { foreach (var action in state.EndOfFileActions) { } }, Throws.Nothing);
                    Assert.That(() => { foreach (var @goto in state.Gotos) { } }, Throws.Nothing);
                    count++;
                }
                Assert.That(count, Is.EqualTo(lr.Count));
            }
        });
    }
}
