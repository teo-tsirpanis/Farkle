// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class EntityHandleTests
{
    [Test]
    public void TestIsKind()
    {
        var tokenSymbolHandle = new TokenSymbolHandle(137);
        var nonterminalHandle = new NonterminalHandle(184);
        var productionHandle = new ProductionHandle(475);
        Assert.Multiple(() =>
        {
            Assert.That(((EntityHandle)tokenSymbolHandle).IsTokenSymbol);
            Assert.That(((EntityHandle)nonterminalHandle).IsNonterminal);
            Assert.That(((EntityHandle)productionHandle).IsProduction);
            Assert.That(((EntityHandle)tokenSymbolHandle).IsNonterminal, Is.False);
            Assert.That(((EntityHandle)nonterminalHandle).IsProduction, Is.False);
            Assert.That(((EntityHandle)productionHandle).IsTokenSymbol, Is.False);
        });
    }

    [Test]
    public void TestSuccessfulCast()
    {
        var tokenSymbolHandle = new TokenSymbolHandle(137);
        var nonterminalHandle = new NonterminalHandle(184);
        var productionHandle = new ProductionHandle(475);
        Assert.Multiple(() =>
        {
            Assert.That((TokenSymbolHandle)(EntityHandle)tokenSymbolHandle, Is.EqualTo(tokenSymbolHandle));
            Assert.That((NonterminalHandle)(EntityHandle)nonterminalHandle, Is.EqualTo(nonterminalHandle));
            Assert.That((ProductionHandle)(EntityHandle)productionHandle, Is.EqualTo(productionHandle));
        });
    }

    [Test]
    public void TestFailedCast()
    {
        var tokenSymbolHandle = new TokenSymbolHandle(137);
        var nonterminalHandle = new NonterminalHandle(184);
        var productionHandle = new ProductionHandle(475);
        Assert.Multiple(() =>
        {
            Assert.That(() => (NonterminalHandle)(EntityHandle)tokenSymbolHandle, Throws.InstanceOf<InvalidCastException>());
            Assert.That(() => (ProductionHandle)(EntityHandle)nonterminalHandle, Throws.InstanceOf<InvalidCastException>());
            Assert.That(() => (TokenSymbolHandle)(EntityHandle)productionHandle, Throws.InstanceOf<InvalidCastException>());
        });
    }

    [Test]
    public void TestNullCast()
    {
        TokenSymbolHandle tokenSymbolHandle = default;
        NonterminalHandle nonterminalHandle = default;
        ProductionHandle productionHandle = default;
        Assert.Multiple(() =>
        {
            Assert.That(() => tokenSymbolHandle.Value, Throws.InstanceOf<InvalidOperationException>());
            Assert.That(() => nonterminalHandle.Value, Throws.InstanceOf<InvalidOperationException>());
            Assert.That(() => productionHandle.Value, Throws.InstanceOf<InvalidOperationException>());
            Assert.That((EntityHandle)tokenSymbolHandle, Is.EqualTo(default(EntityHandle)));
            Assert.That((EntityHandle)nonterminalHandle, Is.EqualTo(default(EntityHandle)));
            Assert.That((EntityHandle)productionHandle, Is.EqualTo(default(EntityHandle)));
            Assert.That(((EntityHandle)tokenSymbolHandle).IsTokenSymbol, Is.False);
            Assert.That(((EntityHandle)nonterminalHandle).IsNonterminal, Is.False);
            Assert.That(((EntityHandle)productionHandle).IsProduction, Is.False);
        });
    }
}
