// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

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
        Grammar.CreateFromFile(GetResourceFile(fileName));

    public static ReusableConstraint IsParserSuccess { get; } = Has.Property(nameof(ParserResult<int>.IsSuccess)).True;
}
