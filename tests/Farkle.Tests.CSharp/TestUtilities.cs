// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tests.CSharp;

public static class TestUtilities
{
    public static IEnumerable<string> Farkle7Grammars => Directory.EnumerateFiles(AppContext.BaseDirectory, "*.grammar.dat");

    public static string GetResourceFile(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
}
