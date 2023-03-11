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
}
