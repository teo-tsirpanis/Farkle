// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Farkle.Analyzers.Tests;

public sealed class FarkleCodeFixTest<TAnalyzer, TCodeFix> : CSharpCodeFixTest<TAnalyzer, TCodeFix, NUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    [StringSyntax("c#-test")]
    public new string TestCode { set => base.TestCode = value; }

    [StringSyntax("c#-test")]
    public new string FixedCode { set => base.FixedCode = value; }

    public FarkleCodeFixTest()
    {
        this.CommonInitialize();
        TestState.Sources.Add(Utilities.EnhancedSyntaxBoilerplate);
        FixedState.Sources.Add(Utilities.EnhancedSyntaxBoilerplate);
        // Because the source generator does not run in these tests, this warning will fire on every production builder factory call.
        // We suppress it.
        DisabledDiagnostics.Add("FARKLE1009");
    }
}
