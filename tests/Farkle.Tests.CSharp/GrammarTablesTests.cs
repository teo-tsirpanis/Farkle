// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class GrammarTablesTests
{
    private static StringHandle DummyStringHandle => new(134);

    [Test]
    public void TestGrammarTable()
    {
        var builder = new GrammarTablesBuilder();
        using var writer = new PooledSegmentBufferWriter<byte>();

        var startSymbol = builder.AddNonterminal(DummyStringHandle, NonterminalAttributes.None, 0);

        Assert.That(() => builder.WriteTo(writer, GrammarHeapSizes.StringHeapSmall), Throws.InvalidOperationException);
        builder.SetGrammarInfo(DummyStringHandle, startSymbol, GrammarAttributes.None);
        Assert.That(() => builder.SetGrammarInfo(DummyStringHandle, startSymbol, GrammarAttributes.None), Throws.InvalidOperationException);
        builder.WriteTo(writer, GrammarHeapSizes.StringHeapSmall);
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

        Assert.That(writer.ToArray(), Is.EqualTo(expectedData));

        var tables = new GrammarTables(expectedData, 0, expectedData.Length, out bool hasUnknownTables);
        Assert.Multiple(() =>
        {
            Assert.That(hasUnknownTables, Is.False);
            Assert.That(tables.GetGrammarName(expectedData), Is.EqualTo(DummyStringHandle));
            Assert.That(tables.GetGrammarStartSymbol(expectedData), Is.EqualTo(startSymbol));
            Assert.That(tables.GetGrammarFlags(expectedData), Is.EqualTo(GrammarAttributes.None));
        });
    }
}
