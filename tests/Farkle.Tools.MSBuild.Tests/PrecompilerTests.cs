// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using NUnit.Framework;

namespace Farkle.Tools.MSBuild.Tests;

public class PrecompilerTests
{
    [Test]
    public void Test()
    {
        var grammar = TestGrammars.GrammarFactory();
        var parser = TestGrammars.ParserFactory();
        var syntaxChecker = TestGrammars.SyntaxCheckerFactory();
        var syntaxChecker2 = TestGrammars.SyntaxCheckerFactory2();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(grammar.Data == parser.GetGrammar().Data);
            Assert.That(grammar.Data == syntaxChecker.GetGrammar().Data);
            Assert.That(grammar.Data == syntaxChecker2.GetGrammar().Data);
            HtmlChecker.Check(grammar);
        }
    }
}
