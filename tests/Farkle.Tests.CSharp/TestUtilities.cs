// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using Farkle.Grammars;
using NUnit.Framework.Constraints;

namespace Farkle.Tests.CSharp;

public static class TestUtilities
{
    private static readonly string ResourcePath = Path.Combine(AppContext.BaseDirectory, "resources");

    public static IEnumerable<string> Farkle7Grammars => Directory.EnumerateFiles(ResourcePath, "*.grammar.dat");

    public static IEnumerable<string> GoldParserGrammars =>
        Directory.EnumerateFiles(ResourcePath, "*.egt")
        // On .NET Framework this apparently also matches .egtn files, which are not supported.
        .Where(x => x.EndsWith(".egt"))
        .Concat(Directory.EnumerateFiles(ResourcePath, "*.cgt"));

    public static string GetResourceFile(string fileName) => Path.Combine(ResourcePath, fileName);

    public static Grammar LoadGrammarFromResource(string fileName) =>
        Grammar.Load(GetResourceFile(fileName));

    /// <param name="modifications">Modifications to the grammar file's bytes, in the form of index-value pairs.</param>
    public static Grammar LoadGrammarFromResource(string fileName, int[] modifications, bool loadUnsafe = false)
    {
        Assert.That(modifications.Length % 2, Is.Zero);
        var grammarBytes = File.ReadAllBytes(GetResourceFile(fileName));
        for (int i = 0; i < modifications.Length; i += 2)
        {
            grammarBytes[modifications[i]] = (byte)modifications[i + 1];
        }

        var immutableBytes = ImmutableCollectionsMarshal.AsImmutableArray(grammarBytes);
        return loadUnsafe ? Grammar.LoadUnsafe(immutableBytes) : Grammar.Load(immutableBytes);
    }

    public static ReusableConstraint IsParserSuccess { get; } = Has.Property(nameof(ParserResult<>.IsSuccess)).True;
}
