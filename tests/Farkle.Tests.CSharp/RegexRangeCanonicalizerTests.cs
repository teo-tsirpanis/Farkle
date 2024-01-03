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

    [TestCase("", true)]
    [TestCase("11", true)]
    [TestCase("12", true)]
    [TestCase("1234", false)]
    [TestCase("1245", true)]
    [TestCase("1223", false)]
    [TestCase("1233", false)]
    [TestCase("1244", true)]
    [TestCase("1423", false)]
    public void TestIsCanonical(string data, bool expectedResult)
    {
        var pairs = MakePairs(data.AsSpan());
        Assert.That(RegexRangeCanonicalizer.IsCanonical(pairs.AsSpan()), Is.EqualTo(expectedResult));
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
    [TestCase("aabbcc", true, "ac")]
    public void TestCanonicalize(string data, bool caseSensitive, string expectedResult)
    {
        var pairs = MakePairs(data.AsSpan());
        var result = RegexRangeCanonicalizer.Canonicalize(pairs, caseSensitive);
        Assert.That(result, Is.EqualTo(MakePairs(expectedResult.AsSpan())).AsCollection);
    }
}
