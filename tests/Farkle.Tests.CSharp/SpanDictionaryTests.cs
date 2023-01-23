// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Collections;

namespace Farkle.Tests.CSharp;

public class SpanDictionaryTests
{
    [Test]
    public void Test()
    {
        var dict = new SpanDictionary<byte, int>();
        dict.Add(""u8, -1);
        dict["aaa"u8] = 137;
        dict["bbb"u8] = 184;
        dict["ccc"u8] = 475;

        Assert.Multiple(() =>
        {
            Assert.That(dict.Count, Is.EqualTo(4));
            Test(""u8, -1);
            Test("aaa"u8, 137);
            Test("bbb"u8, 184);
            Test("ccc"u8, 475);
        });

        dict["aaa"u8] = 03;
        Test("aaa"u8, 03);
        Assert.Multiple(() =>
        {
            Assert.That(() => dict.Add("ccc"u8, 03), Throws.ArgumentException); // Of course it would fail…
            Assert.That(() => dict["ddd"u8], Throws.InstanceOf<KeyNotFoundException>());
            Assert.That(dict.TryGetValue("ddd"u8, out _), Is.False);
        });

        void Test(ReadOnlySpan<byte> key, int expectedValue) =>
            Assert.That(dict[key], Is.EqualTo(expectedValue));
    }

    [Test]
    public void TestCollisions()
    {
        var dict = new SpanDictionary<sbyte, int>();
        // In debug mode these keys will have the same hash code.
        dict.Add(new sbyte[] { 1, 2 }, 1);
        dict.Add(new sbyte[] { 2, 1 }, 2);

        Assert.Multiple(() =>
        {
            Assert.That(dict[new sbyte[] { 1, 2 }], Is.EqualTo(1));
            Assert.That(dict[new sbyte[] { 2, 1 }], Is.EqualTo(2));
        });
    }
}
