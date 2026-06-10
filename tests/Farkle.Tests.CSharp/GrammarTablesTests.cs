// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars;
using Farkle.Grammars.Writers;

namespace Farkle.Tests.CSharp;

internal class GrammarTablesTests
{
    private static StringHandle DummyStringHandle => new(134);

    [Test]
    public void TestGrammarTable()
    {
        var writer = new GrammarTablesWriter();
        using var buffer = new PooledSegmentBufferWriter<byte>();

        var startSymbol = writer.AddNonterminal(DummyStringHandle, NonterminalAttributes.None, 0);

        writer.SetGrammarInfo(DummyStringHandle, startSymbol, GrammarAttributes.None);
        Assert.That(() => writer.SetGrammarInfo(DummyStringHandle, startSymbol, GrammarAttributes.None), Throws.InvalidOperationException);
        writer.WriteTo(buffer, GrammarHeapSizes.StringHeapSmall);
        byte[] expectedData = new byte[]
        {
            0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // TablesPresent
            0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, // RowCounts
            0x05, 0x05, // RowSizes
            0x01, // HeapSizes
            0x00, 0x00, 0x00, 0x00, 0x00, // Padding
            0x86, 0x00, 0x01, 0x00, 0x00, // Grammar
            0x86, 0x00, 0x00, 0x00, 0x01 // Nonterminal
        };

        Assert.That(buffer.ToArray(), Is.EqualTo(expectedData));

        var tables = new GrammarTables(expectedData, new(0, expectedData.Length), out bool hasUnknownTables);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(hasUnknownTables, Is.False);
            Assert.That(tables.GetGrammarName(expectedData), Is.EqualTo(DummyStringHandle));
            Assert.That(tables.GetGrammarStartSymbol(expectedData), Is.EqualTo(startSymbol));
            Assert.That(tables.GetGrammarFlags(expectedData), Is.EqualTo(GrammarAttributes.None));
        }
    }

    [Test]
    public void TestTokenSymbolTable()
    {
        var writer = new GrammarTablesWriter();

        writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.Terminal);
        // A terminal can't start a group.
        Assert.That(() => writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.Terminal | TokenSymbolAttributes.GroupStart), Throws.ArgumentException);
        writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.None);
        // Terminals must be together at the beginning.
        Assert.That(() => writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.Terminal), Throws.InvalidOperationException);

        for (int i = 0; i < 0xF_FFFD; i++)
        {
            writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.None);
        }

        // We can add up to 2^20 - 1 token symbols.
        Assert.That(() => writer.AddTokenSymbol(DummyStringHandle, TokenSymbolAttributes.None), Throws.InvalidOperationException);
    }

    [Test]
    public void TestNonterminalsWithNoProductions()
    {
        var writer = new GrammarTablesWriter();
        using var buffer = new PooledSegmentBufferWriter<byte>();

        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 1);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 2);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddNonterminal(default, NonterminalAttributes.None, 1);
        writer.AddNonterminal(default, NonterminalAttributes.None, 0);
        writer.AddProduction(0);
        writer.AddProduction(0);
        writer.AddProduction(0);
        writer.AddProduction(0);
        writer.WriteTo(buffer, 0);
        var bytes = buffer.ToArray();

        var tables = new GrammarTables(bytes, new(0, bytes.Length), out _);
        tables.ValidateContent(bytes, default, default);
    }
}
