// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class GrammarTests
{
    [TestCase("legacy.cgt", GrammarFileType.Cgt)]
    [TestCase("JSON.egt", GrammarFileType.Egt5)]
    [TestCase("JSON.egtn", GrammarFileType.EgtNeo)]
    public void TestInvalidFiles(string fileName, GrammarFileType fileType)
    {
        var buffer = File.ReadAllBytes(TestUtilities.GetResourceFile(fileName));
        var br = new BufferReader(buffer);
        var header = Grammar.ReadHeader(ref br);
        Assert.Multiple(() =>
        {
            Assert.That(header.IsSupported, Is.False);
            Assert.That(header.FileType, Is.EqualTo(fileType));
        });
    }
}
