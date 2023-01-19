// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Tests.CSharp;

public class BufferReaderTests
{
    [Test]
    public void TestReadNumbers()
    {
        var br = new BufferReader(new byte[] { 184, 0, 219, 1 });
        Assert.That(br.ReadUInt16(), Is.EqualTo((ushort)184));
        Assert.That(br.ReadUInt16(), Is.EqualTo((ushort)475));
    }

    [Test]
    public void TestIncompleteBuffers()
    {
        var br = new BufferReader(stackalloc byte[16]);
        Assert.That(br.ReadBytes(10).Length, Is.EqualTo(10));
        Assert.That(br.RemainingBuffer.Length, Is.EqualTo(6));
        Assert.That(br.ReadBytes(10).Length, Is.EqualTo(6));
        Assert.That(br.RemainingBuffer.IsEmpty);
    }

    [Test]
    public unsafe void TestAdvanceBy()
    {
        var br = new BufferReader(stackalloc byte[16]);
        Assert.That(br.RemainingBuffer.Length, Is.EqualTo(16));
        br.AdvanceBy(10);
        BufferReader* ptr = &br;
        Assert.Multiple(() =>
        {
            Assert.That(ptr->RemainingBuffer.Length, Is.EqualTo(6));
            Assert.That(() => ptr->AdvanceBy(10), Throws.InstanceOf<EndOfStreamException>());
        });
    }
}
