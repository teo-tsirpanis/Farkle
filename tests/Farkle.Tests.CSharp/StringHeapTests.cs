// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class StringHeapTests
{
    [Test]
    public void TestWriteAndRead()
    {
        StringHeapBuilder builder = new();
        StringHandle emptyHandle = builder.Add("");
        StringHandle aaaHandle = builder.Add("aaa");
        StringHandle bbbHandle = builder.Add("bbb");
        StringHandle cccHandle = builder.Add("ccc");
        Assert.Multiple(() =>
        {
            Assert.That(() => builder.Add("Hello\0"), Throws.ArgumentException);
            Assert.That(builder.Add("aaa"), Is.EqualTo(aaaHandle));
            Assert.That(emptyHandle.IsNil);
            Assert.That(bbbHandle, Is.Not.EqualTo(aaaHandle));
            Assert.That(cccHandle, Is.Not.EqualTo(aaaHandle));
        });

        byte[] actualHeap;
        using (var bufferWriter = new PooledSegmentBufferWriter<byte>())
        {
            builder.WriteTo(bufferWriter);
            actualHeap = bufferWriter.ToArray();
        }

        Assert.That(actualHeap, Is.EqualTo("\0aaa\0bbb\0ccc\0"u8.ToArray()));

        var heap = new StringHeap(actualHeap, 0, actualHeap.Length);
        Assert.Multiple(() =>
        {
            Assert.That(heap.GetString(actualHeap, default), Is.Empty);
            Assert.That(heap.GetString(actualHeap, aaaHandle), Is.EqualTo("aaa"));
            Assert.That(heap.GetString(actualHeap, bbbHandle), Is.EqualTo("bbb"));
            Assert.That(heap.GetString(actualHeap, cccHandle), Is.EqualTo("ccc"));
            Assert.That(() => heap.GetString(actualHeap, new StringHandle(aaaHandle.Value + 1)), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => heap.GetString(actualHeap, new StringHandle(184)), Throws.InstanceOf<ArgumentOutOfRangeException>());

            Assert.That(heap.LookupString(actualHeap, "".AsSpan()).IsNil);
            Assert.That(heap.LookupString(actualHeap, "aaa".AsSpan()), Is.EqualTo(aaaHandle));
            Assert.That(heap.LookupString(actualHeap, "bbb".AsSpan()), Is.EqualTo(bbbHandle));
            Assert.That(heap.LookupString(actualHeap, "ccc".AsSpan()), Is.EqualTo(cccHandle));
            Assert.That(heap.LookupString(actualHeap, "ddd".AsSpan()).IsNil);
        });
    }

    [Test]
    public void TestEmptyHeap()
    {
        var heap = new StringHeap(default, 0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(heap.GetString(default, default), Is.EqualTo(""));
            Assert.That(() => heap.GetString(default, new StringHandle(184)), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void TestAddInvalidStrings()
    {
        StringHeapBuilder builder = new();
        Assert.Multiple(() =>
        {
            Assert.That(() => builder.Add("\0"), Throws.ArgumentException);
            Assert.That(() => builder.Add("\uD8B8"), Throws.ArgumentException);
        });
    }

    [Test]
    public void TestInvalidHeaps()
    {
        Assert.Multiple(() =>
        {
            // No leading zero.
            Test("abc\0"u8);
            // No trailing zero.
            Test("\0abc"u8);
        });

        static unsafe void Test(ReadOnlySpan<byte> heap)
        {
            ReadOnlySpan<byte>* heapPtr = &heap;
            Assert.That(() => new StringHeap(*heapPtr, 0, heapPtr->Length), Throws.InstanceOf<InvalidDataException>());
        }
    }

    [Test]
    public void TestBuildEmptyHeap()
    {
        StringHeapBuilder builder = new();
        builder.Add("");
        using var bw = new PooledSegmentBufferWriter<byte>();
        builder.WriteTo(bw);
        Assert.That(bw.WrittenCount, Is.Zero);
    }
}
