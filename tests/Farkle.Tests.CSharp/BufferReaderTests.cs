// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;

namespace Farkle.Tests.CSharp
{
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
            Assert.That(br.Buffer.Length, Is.EqualTo(6));
            Assert.That(br.ReadBytes(10).Length, Is.EqualTo(6));
            Assert.That(br.Buffer.IsEmpty);
        }

        [Test]
        public void TestAdvanceBy()
        {
            var br = new BufferReader(stackalloc byte[16]);
            Assert.That(br.Buffer.Length, Is.EqualTo(16));
            br.AdvanceBy(10);
            Assert.That(br.Buffer.Length, Is.EqualTo(6));

            bool threw = false;
            try
            {
                br.AdvanceBy(10);
            }
            catch (Exception e)
            {
                Assert.That(e, Is.InstanceOf<EndOfStreamException>());
                threw = true;
            }
            Assert.That(threw);
        }
    }
}
