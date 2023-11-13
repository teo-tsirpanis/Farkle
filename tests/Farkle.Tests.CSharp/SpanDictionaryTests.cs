// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Collections;
using System.Collections.Immutable;

namespace Farkle.Tests.CSharp;

internal class SpanDictionaryTests
{
    [Test]
    public void Test()
    {
        var dict = new BlobDictionary<int>();
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
        var dict = new CollidingBlobDictionary<int>();
        dict.Add("ab"u8, 1);
        dict.Add("ba"u8, 2);

        Assert.Multiple(() =>
        {
            Assert.That(dict["ab"u8], Is.EqualTo(1));
            Assert.That(dict["ba"u8], Is.EqualTo(2));
        });
    }

    private sealed class CollidingBlobDictionary<TValue> : SpanDictionaryBase<byte, ImmutableArray<byte>, TValue>
    {
        protected override ImmutableArray<byte> ToContainer(ReadOnlySpan<byte> key) => key.ToImmutableArray();

        protected override int GetHashCode(ReadOnlySpan<byte> key) => 0;

        protected override ReadOnlySpan<byte> AsSpan(ImmutableArray<byte> container) => container.AsSpan();
    }
}
