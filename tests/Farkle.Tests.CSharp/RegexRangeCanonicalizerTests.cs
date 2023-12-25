// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Builder.StateMachines;

namespace Farkle.Tests.CSharp;

internal class RegexRangeCanonicalizerTests
{
    private static (T, T)[] MakePairs<T>(ReadOnlySpan<T> data)
    {
        Debug.Assert(data.Length % 2 == 0);
        var result = new (T, T)[data.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (data[i * 2], data[i * 2 + 1]);
        }
        return result;
    }

    [TestCase(new int[] { }, true)]
    [TestCase(new[] { 1, 1 }, true)]
    [TestCase(new[] { 1, 2 }, true)]
    [TestCase(new[] { 1, 2, 3, 4 }, true)]
    [TestCase(new[] { 1, 2, 2, 3 }, false)]
    [TestCase(new[] { 1, 2, 3, 3 }, true)]
    [TestCase(new[] { 1, 4, 2, 3 }, false)]
    public void TestIsCanonical(int[] data, bool expectedResult)
    {
        var pairs = MakePairs<int>(data.AsSpan());
        Assert.That(RegexRangeCanonicalizer.IsCanonical<int>(pairs.AsSpan()), Is.EqualTo(expectedResult));
    }

    [TestCase("", true, "")]
    [TestCase("", false, "")]
    [TestCase("033779", true, "09")]
    [TestCase("043879", true, "09")]
    [TestCase("092468", true, "09")]
    [TestCase("az", true, "az")]
    [TestCase("az", false, "AZaz")]
    [TestCase("aaeeiioouu", false, "AAEEIIOOUUaaeeiioouu")]
    [TestCase("aabbeeiioouu", false, "ABEEIIOOUUabeeiioouu")]
    public void TestCanonicalize(string data, bool caseSensitive, string expectedResult)
    {
        var pairs = MakePairs(data.AsSpan());
        var result = RegexRangeCanonicalizer.Canonicalize(pairs, caseSensitive);
        Assert.That(result, Is.EqualTo(MakePairs(expectedResult.AsSpan())).AsCollection);
    }
}
