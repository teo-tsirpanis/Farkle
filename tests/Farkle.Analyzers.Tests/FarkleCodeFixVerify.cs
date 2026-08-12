// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Farkle.Analyzers.Tests;

public class FarkleCodeFixVerify<TAnalyzer, TCodeFix> : CodeFixVerifier<TAnalyzer, TCodeFix, FarkleCodeFixTest<TAnalyzer, TCodeFix>, NUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public static new Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, [StringSyntax("c#-test")] string fixedSource) =>
        CodeFixVerifier<TAnalyzer, TCodeFix, FarkleCodeFixTest<TAnalyzer, TCodeFix>, NUnitVerifier>.VerifyCodeFixAsync(source, fixedSource);
}
