// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Farkle.Analyzers.Tests;

public class FarkleAnalyzerVerifier<TAnalyzer> : AnalyzerVerifier<TAnalyzer, FarkleAnalyzerTest<TAnalyzer>, NUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static new Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, params DiagnosticResult[] expected) =>
        AnalyzerVerifier<TAnalyzer, FarkleAnalyzerTest<TAnalyzer>, NUnitVerifier>.VerifyAnalyzerAsync(source, expected);
}
