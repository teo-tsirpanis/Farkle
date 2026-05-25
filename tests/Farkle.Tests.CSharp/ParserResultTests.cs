// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT


namespace Farkle.Tests.CSharp;

internal class ParserResultTests
{
    [Test]
    public void TestToString()
    {
        ParserResult<int> success = ParserResult.CreateSuccess(42);
        ParserResult<int> failure = ParserResult.CreateError<int>("error");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(success.ToString(), Is.EqualTo("42"));
            Assert.That(failure.ToString(), Is.EqualTo("error"));
        }
    }
}
