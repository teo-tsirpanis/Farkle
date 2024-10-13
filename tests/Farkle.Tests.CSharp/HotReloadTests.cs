// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NET6_0_OR_GREATER
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
#endif
using System.Buffers;
using Farkle.Builder;
using Farkle.HotReload;
using Farkle.Parser;
using Farkle.Parser.Tokenizers;

namespace Farkle.Tests.CSharp;

internal class HotReloadTests
{
#if NET6_0_OR_GREATER
    [Test]
    public void TestAssemblyAttributes()
    {
        var farkleAssembly = typeof(CharParser).Assembly;
        Assert.That(farkleAssembly.GetCustomAttributes<MetadataUpdateHandlerAttribute>(),
            Has.One.Matches((MetadataUpdateHandlerAttribute attr) => attr.HandlerType == typeof(MetadataUpdatableManager)));
    }

    [Test]
    public void TestMetadataUpdatableManager()
    {
        int clearCacheCount1 = 0, clearCacheCount2 = 0;
        var mdUpdatable1 = new MetadataUpdatable(() => clearCacheCount1++);
        var mdUpdatable2 = new MetadataUpdatable(() => clearCacheCount2++);
        MetadataUpdatableManager.Register(typeof(DummyType1), mdUpdatable1);
        MetadataUpdatableManager.Register(typeof(DummyType2), mdUpdatable2);

        // Clear metadata on only one type.
        MetadataUpdatableManager.ClearCache([typeof(DummyType1)]);
        Assert.That((clearCacheCount1, clearCacheCount2), Is.EqualTo((1, 0)));
        MetadataUpdatableManager.ClearCache([typeof(DummyType2)]);
        Assert.That((clearCacheCount1, clearCacheCount2), Is.EqualTo((1, 1)));
        // All types.
        MetadataUpdatableManager.ClearCache(null);
        Assert.That((clearCacheCount1, clearCacheCount2), Is.EqualTo((2, 2)));
        // No types.
        MetadataUpdatableManager.ClearCache([]);
        Assert.That((clearCacheCount1, clearCacheCount2), Is.EqualTo((2, 2)));
        // An irrelevant type.
        MetadataUpdatableManager.ClearCache([typeof(CharParser)]);
        Assert.That((clearCacheCount1, clearCacheCount2), Is.EqualTo((2, 2)));

        // The metadata updatable objects might be collected by the GC.
        GC.KeepAlive(mdUpdatable1);
        GC.KeepAlive(mdUpdatable2);
    }

    [Test]
    public void TestMetadataUpdatableManagerCollectible()
    {
        // Create and register a metadata updatable object and check that
        // it can be collected by the GC.
        var weakRef = CreateAndRegisterMetadataUpdatable();
        int gcCollectTries = 10;
        while (weakRef.IsAlive && gcCollectTries-- > 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.That(weakRef.IsAlive, Is.False);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference CreateAndRegisterMetadataUpdatable()
        {
            var mdUpdatable = new MetadataUpdatable(() => { });
            MetadataUpdatableManager.Register(typeof(DummyType1), mdUpdatable);
            return new WeakReference(mdUpdatable);
        }
    }

    private sealed class MetadataUpdatable(Action fClearCache) : IMetadataUpdatable
    {
        public void ClearCache() => fClearCache();
    }

    private sealed class DummyType1;

    private sealed class DummyType2;
#endif

    [Test]
    public void TestMetadataUpdatableParser()
    {
        var realParser = Terminal.Literal("aaa").BuildSyntaxCheck();
        CharParser<object?> mdUpdatableParser = MetadataUpdatableParser.Create(typeof(HotReloadTests), () => realParser);

        // Check clearing the cache without changing the real parser.
        Assert.That(mdUpdatableParser.GetGrammar(), Is.EqualTo(realParser.GetGrammar()));
        ClearCache();
        Assert.That(mdUpdatableParser.GetGrammar(), Is.EqualTo(realParser.GetGrammar()));

        // Check clearing the cache
        realParser = Terminal.Literal("bbb").BuildSyntaxCheck();
        ClearCache();
        Assert.That(mdUpdatableParser.GetGrammar(), Is.EqualTo(realParser.GetGrammar()));

        // Check changing the tokenizer.
        var newTokenizer = Tokenizer.Create<char>(realParser.GetGrammar());
        mdUpdatableParser = mdUpdatableParser.WithTokenizer(newTokenizer);
        Assert.That(mdUpdatableParser.GetTokenizer(), Is.EqualTo(newTokenizer));
        ClearCache();
        Assert.That(mdUpdatableParser.GetTokenizer(), Is.EqualTo(newTokenizer));

        void ClearCache() => ((IMetadataUpdatable) mdUpdatableParser).ClearCache();
    }

    [Test]
    public void TestParserStateContext()
    {
        // Test that Hot Reload does not affect existing parser state contexts.
        var realParser = Terminal.Literal("aaa").BuildSyntaxCheck();
        var mdUpdatableParser = MetadataUpdatableParser.Create(typeof(HotReloadTests), () => realParser);
        var context1 = ParserStateContext.Create(mdUpdatableParser);

        realParser = Terminal.Literal("bbb").BuildSyntaxCheck();
        mdUpdatableParser.ClearCache();
        var context2 = ParserStateContext.Create(mdUpdatableParser);

        WriteSpan(context1, "aaa");
        context1.CompleteInput();
        WriteSpan(context2, "bbb");
        context2.CompleteInput();

        Assert.Multiple(() =>
        {
            Assert.That(context1.Result, TestUtilities.IsParserSuccess);
            Assert.That(context2.Result, TestUtilities.IsParserSuccess);
        });

        static void WriteSpan(IBufferWriter<char> bufferWriter, string str)
        {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            bufferWriter.Write(str);
#else
            str.AsSpan().CopyTo(bufferWriter.GetSpan(str.Length));
            bufferWriter.Advance(str.Length);
#endif
        }
    }
}
