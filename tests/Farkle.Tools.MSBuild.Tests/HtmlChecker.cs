// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using System.IO;
using HtmlAgilityPack;
using Xunit;

namespace Farkle.Tools.MSBuild.Tests
{
    public static class HtmlChecker
    {
        public static void Check(Grammars.Grammar grammar)
        {
            var grammarName = grammar.Properties.Name;
            var htmlPath = Path.ChangeExtension(Path.Join(AppContext.BaseDirectory, grammarName), ".html");
            Assert.True(File.Exists(htmlPath), $"File '{htmlPath}' does not exist.");

            var doc = new HtmlDocument();
            doc.Load(htmlPath);

            Assert.Empty(doc.ParseErrors);

            Assert.All(grammar.Symbols.Nonterminals, x => AssertHasId($"n{x.Index}"));
            Assert.All(grammar.Productions, x => AssertHasId($"prod{x.Index}"));
            Assert.All(grammar.LALRStates, x => AssertHasId($"lalr{x.Index}"));
            Assert.All(grammar.DFAStates, x => AssertHasId($"dfa{x.Index}"));

            void AssertHasId(string id) => Assert.NotNull(doc.GetElementbyId(id));
        }
    }
}
