// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT


using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using HtmlAgilityPack;
using NUnit.Framework;

namespace Farkle.Tools.MSBuild.Tests;

public static class HtmlChecker
{
    public static void Check(Grammar grammar)
    {
        var grammarName = grammar.GrammarInfo.Name;
        var htmlPath = Path.ChangeExtension(Path.Join(AppContext.BaseDirectory, grammarName), ".html");
        Assert.That(htmlPath, Does.Exist);

        var doc = new HtmlDocument();
        doc.Load(htmlPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(doc.ParseErrors, Is.Empty);
            Assert.That(grammar.Nonterminals, Has.All.Matches<NonterminalDefinition>(x => doc.GetElementbyId($"n{x.Handle.Value}") is not null));
            Assert.That(grammar.Productions, Has.All.Matches<ProductionDefinition>(x => doc.GetElementbyId($"prod{x.Handle.Value}") is not null));
            Assert.That(grammar.LrStateMachine, Is.Not.Null.And.All.Matches<LrState>(x => doc.GetElementbyId($"lalr{x.StateIndex}") is not null));
            Assert.That(grammar.DfaOnChar, Is.Not.Null.And.All.Matches<DfaState<char>>(x => doc.GetElementbyId($"dfa{x.StateIndex}") is not null));
        }
    }
}
