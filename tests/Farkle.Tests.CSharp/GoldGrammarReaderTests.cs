// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars.GoldParser;
using System.Text;

namespace Farkle.Tests.CSharp;

internal class GoldGrammarReaderTests
{
    [TestCase("", "")]
    [TestCase("a", "aa")]
    [TestCase("abcdefu", "afuu")]
    public void TestConvertCgtCharacterSet(string characterSet, string expected)
    {
        var convertedSet = GoldGrammarReader.ConvertCgtCharacterSet(characterSet);

        var sb = new StringBuilder(convertedSet.Length * 2);
        foreach (var (start, end) in convertedSet)
        {
            sb.Append(start).Append(end);
        }

        Assert.That(sb.ToString(), Is.EqualTo(expected));
    }

    [TestCase("aa")]
    [TestCase("ba")]
    public void TestInvalidCgtCharacterSet(string characterSet)
    {
        Assert.That(() => GoldGrammarReader.ConvertCgtCharacterSet(characterSet), Throws.InstanceOf<InvalidDataException>());
    }
}
