// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser;

namespace Farkle.Tests.CSharp;

internal class PositionTrackerTests
{
    public static void TestLineEndings<T>(ReadOnlySpan<T> text)
    {
        TextPosition[] expectedPositions = {
            TextPosition.Initial,
            TextPosition.Create1(2,2),
            TextPosition.Create1(3,1),
            TextPosition.Create1(3,1),
            TextPosition.Create1(4,1)
        };

        TextPosition[] actualPositions = new TextPosition[text.Length];

        PositionTracker tracker = new();
        for (int i = 0; i < actualPositions.Length; i++)
        {
            tracker.Advance(text.Slice(i, 1));
            actualPositions[i] = tracker.Position;
        }

        Assert.That(actualPositions, Is.EqualTo(expectedPositions));
    }

    [Test]
    public void TestLineEndingsChar() => TestLineEndings("\r \n\r\n".AsSpan());

    [Test]
    public void TestLineEndingsByte() => TestLineEndings("\r \n\r\n"u8);

    public void TestFinalCr<T>(ReadOnlySpan<T> text)
    {
        PositionTracker tracker = new();
        tracker.Advance(text);
        Assert.That(tracker.Position, Is.EqualTo(TextPosition.Create1(1, 2)));
        tracker.CompleteInput();
        Assert.That(tracker.Position, Is.EqualTo(TextPosition.Create1(2, 1)));
    }

    [Test]
    public void TestFinalCrChar() => TestFinalCr(" \r".AsSpan());

    [Test]
    public void TestFinalCrByte() => TestFinalCr(" \r"u8);
}
