// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

public static class TestUtilities
{
    private static readonly string ResourcePath = Path.Combine(AppContext.BaseDirectory, "resources");

    public static IEnumerable<string> Farkle7Grammars => Directory.EnumerateFiles(ResourcePath, "*.grammar.dat");

    public static string GetResourceFile(string fileName) => Path.Combine(ResourcePath, fileName);

    public static Grammar LoadGrammarFromResource(string fileName) =>
        Grammar.Create(File.ReadAllBytes(GetResourceFile(fileName)));
}
