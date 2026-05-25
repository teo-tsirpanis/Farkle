// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Assertion", "NUnit2010:Use EqualConstraint for better assertion messages in case of failure", Justification = "We specifically want to test union matching to null.")]
internal class EntityHandleTests
{
    [Test]
    public void TestIsKind()
    {
        var tokenSymbolHandle = new TokenSymbolHandle(137);
        var nonterminalHandle = new NonterminalHandle(184);
        var productionHandle = new ProductionHandle(475);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((SymbolHandle)tokenSymbolHandle) is TokenSymbolHandle);
            Assert.That(((EntityHandle)nonterminalHandle).IsNonterminal);
            Assert.That(((EntityHandle)productionHandle).IsProduction);
            Assert.That(((EntityHandle)tokenSymbolHandle).IsNonterminal, Is.False);
            Assert.That(((EntityHandle)nonterminalHandle).IsProduction, Is.False);
            Assert.That(((EntityHandle)productionHandle).IsTokenSymbol, Is.False);
        }
    }

    [Test]
    public void TestMatching()
    {
        SymbolHandle tokenSymbolHandle = new TokenSymbolHandle(137);
        SymbolHandle nonterminalHandle = new NonterminalHandle(184);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokenSymbolHandle is TokenSymbolHandle);
            Assert.That(tokenSymbolHandle is not NonterminalHandle);
            Assert.That(tokenSymbolHandle is not null);
            Assert.That(nonterminalHandle is NonterminalHandle);
            Assert.That(nonterminalHandle is not TokenSymbolHandle);
            Assert.That(nonterminalHandle is not null);
        }
    }

    [Test]
    public void TestFailedCast()
    {
        var tokenSymbolHandle = new TokenSymbolHandle(137);
        var nonterminalHandle = new NonterminalHandle(184);
        var productionHandle = new ProductionHandle(475);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => (NonterminalHandle)(EntityHandle)tokenSymbolHandle, Throws.InstanceOf<InvalidCastException>());
            Assert.That(() => (ProductionHandle)(EntityHandle)nonterminalHandle, Throws.InstanceOf<InvalidCastException>());
            Assert.That(() => (TokenSymbolHandle)(EntityHandle)productionHandle, Throws.InstanceOf<InvalidCastException>());
        }
    }

    [Test]
    public void TestNullCast()
    {
        SymbolHandle defaultHandle = default;
        SymbolHandle fromTokenSymbol = new((TokenSymbolHandle)default);
        SymbolHandle fromNonterminal = new((NonterminalHandle)default);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaultHandle.Value, Is.Null);
            Assert.That(fromTokenSymbol.Value, Is.Null);
            Assert.That(fromNonterminal.Value, Is.Null);
            Assert.That(defaultHandle is null);
            Assert.That(fromTokenSymbol is null);
            Assert.That(fromNonterminal is null);
        }
    }
}
