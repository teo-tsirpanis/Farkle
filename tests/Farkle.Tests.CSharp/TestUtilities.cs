// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tests.CSharp;

public static class TestUtilities
{
    public static string GetResourceFile(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
}
