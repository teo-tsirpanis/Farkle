// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Security.Cryptography;

namespace Farkle.Tests.CSharp;

internal class PooledSegmentBufferWriterTests
{
    [Test]
    public void TestExceptionalCases()
    {
        using var bw = new PooledSegmentBufferWriter<byte>(0);
        
        Assert.That(() => bw.Advance(184), Throws.InstanceOf<ArgumentOutOfRangeException>());
        _ = bw.GetMemory(1);
        Assert.That(() => bw.Advance(int.MaxValue), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void TestClearsObjects()
    {
        using var bw = new PooledSegmentBufferWriter<object>();
        ref object obj = ref bw.GetMemory().Span[0];
        obj = new object();
        bw.Advance(1);
        bw.Clear();
        Assert.That(obj, Is.Null);
    }
    
    [TestCase(10)]
    [TestCase(100)]
    public void TestAlternatingLengthWrites(int totalWrites)
    {
        var bw = new PooledSegmentBufferWriter<byte>();

        Run();
        bw.Clear();
        bw.Clear();
        CollectionAssert.IsEmpty(bw.ToArray());
        Run();
        bw.Dispose();
        bw.Dispose();
        bw.Clear();
        CollectionAssert.IsEmpty(bw.ToArray());
        Run();

        void Run()
        {
            byte[] data = GetRandomArray(totalWrites * (totalWrites + 1) / 2); // 1 + 2 … totalWrites
            int bytesWritten = 0;

            LinkedList<int> writeSizes = new(Enumerable.Range(1, totalWrites));
            bool front = true;

            while (writeSizes.Count > 0)
            {
                int size;
                if (front)
                {
                    size = writeSizes.First();
                    writeSizes.RemoveFirst();
                }
                else
                {
                    size = writeSizes.Last();
                    writeSizes.RemoveLast();
                }
                front = !front;

                bw.Write(data.AsSpan(bytesWritten, size));
                bytesWritten += size;
            }

            byte[] readData = bw.ToArray();
            CollectionAssert.AreEqual(data, readData);
        }
    }

    private static byte[] GetRandomArray(int length)
    {
        var result = new byte[length];
        RandomNumberGenerator.Create().GetBytes(result);
        return result;
    }
}
