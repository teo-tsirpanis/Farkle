// Copyright 2023 Theodore Tsirpanis.
// SPDX-License-Identifier: Apache-2.0

namespace Farkle.Tests.CSharp;

internal class ParserResultTests
{
    [Test]
    public void TestToString()
    {
        ParserResult<int> success = ParserResult.CreateSuccess(42);
        ParserResult<int> failure = ParserResult.CreateError<int>("error");
        Assert.Multiple(() =>
        {
            Assert.That(success.ToString(), Is.EqualTo("42"));
            Assert.That(failure.ToString(), Is.EqualTo("error"));
        });
    }
}
