// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;

namespace Farkle.Analyzers.Tests;

public class FarkleSourceGeneratorTest<TSourceGenerator> : CSharpSourceGeneratorTest<TSourceGenerator, NUnitVerifier>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public FarkleSourceGeneratorTest()
    {
        this.CommonInitialize();
    }

    protected override ParseOptions CreateParseOptions() =>
        // Use the C# language version corresponding to the earliest framework we target.
        // TODO-NET8: Change to C# 14 when we drop .NET 8 support.
        ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion.CSharp12);
}
