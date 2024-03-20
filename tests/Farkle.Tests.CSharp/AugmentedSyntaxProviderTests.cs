// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Tests.CSharp;

internal class AugmentedSyntaxProviderTests
{
    [Test]
    public void TestSymbolRoundTrip([Values(0, 1, 1 << 20)] int index, [Values] bool isTerminal)
    {
        // We don't bother with creating a dummy IGrammarSyntaxProvider,
        // it gets used only for debugger displaying.
        var symbol = Symbol.Create((index, isTerminal), default);

        Assert.Multiple(() =>
        {
            Assert.That(symbol.IsTerminal, Is.EqualTo(isTerminal));
            Assert.That(symbol.Index, Is.EqualTo(index));
        });
    }

    [Test]
    public void TestSymbolOrder([Values(0, 1, (1 << 20) - 1)] int index)
    {
        var terminal = Symbol.CreateTerminal(index, default);
        var nextTerminal = Symbol.CreateTerminal(index + 1, default);
        var nonterminal = Symbol.CreateNonterminal(index, default);
        var nextNonterminal = Symbol.CreateNonterminal(index + 1, default);

        Assert.That((Symbol[])[terminal, nextTerminal, nonterminal, nextNonterminal], Is.Ordered);
    }
}
