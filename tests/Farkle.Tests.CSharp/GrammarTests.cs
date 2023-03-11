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

    [TestCase("JSON")]
    [TestCase("COBOL85")]
    public void TestGoldParserConversion(string grammarName)
    {
        var convertedFromEgt = ConvertGrammarFile(TestUtilities.GetResourceFile(Path.ChangeExtension(grammarName, ".egt")));
        var convertedFromCgt = ConvertGrammarFile(TestUtilities.GetResourceFile(Path.ChangeExtension(grammarName, ".cgt")));
        var originalFarkleGrammar = File.ReadAllBytes(TestUtilities.GetResourceFile(Path.ChangeExtension(grammarName, ".grammar.dat")));

        Assert.Multiple(() =>
        {
            Assert.That(convertedFromEgt, Is.EqualTo(originalFarkleGrammar));
            Assert.That(convertedFromCgt, Is.EqualTo(originalFarkleGrammar));
        });

        static ImmutableArray<byte> ConvertGrammarFile(string path)
        {
            using var stream = File.OpenRead(path);
            return GoldGrammarConverter.Convert(GoldGrammarReader.ReadGrammar(stream));
        }
    }
}
