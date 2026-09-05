// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers.Tests;

public sealed class FarkleAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, NUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public FarkleAnalyzerTest()
    {
        this.CommonInitialize();
        TestState.Sources.Add(Utilities.EnhancedSyntaxBoilerplate);
        // Because the source generator does not run in these tests, this warning will fire on every production builder factory call.
        // We suppress it.
        DisabledDiagnostics.Add("FARKLE1009");
    }

    protected override ParseOptions CreateParseOptions() =>
        // Use the C# language version corresponding to the earliest framework we target.
        // TODO-NET8: Change to C# 14 when we drop .NET 8 support.
        ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion.CSharp12);
}
