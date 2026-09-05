// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using VerifyCS = Farkle.Analyzers.Tests.FarkleAnalyzerVerifier<Farkle.Analyzers.EnhancedSyntax.ProductionBuilderFactoryAnalyzer>;

namespace Farkle.Analyzers.Tests;

public class ProductionBuilderFactoryAnalyzerTests
{
    [Test]
    public async Task TestHappyPath()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        string str = null;
        IGrammarSymbol<int> symbol = null;
        IGrammarSymbol symbolU = null;

        _ = Production.Build(str, symbol, symbolU);
        _ = Production.Build(str, str, str);
        _ = Production.Build(symbol, symbol, symbol);
        _ = Production.Build(symbol, symbolU, symbolU);
        """);
    }

    [Test]
    public async Task TestImplicitlyConvertibleToString()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        _ = Production.Build(default(MyString));

        struct MyString
        {
            public static implicit operator string(MyString myString) => null;
        }
        """);
    }

    [Test]
    public async Task TestUnsupportedType()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        int i = 670;
        IGrammarBuilder builder = null;

        _ = Production.Build({|#0:i|});
        _ = Production.Build({|#1:builder|});
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionBuilderFactoryUnsupportedType).WithLocation(0).WithArguments("0", "int"),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionBuilderFactoryUnsupportedType).WithLocation(1).WithArguments("0", "Farkle.Builder.IGrammarBuilder"));
    }

    [Test]
    public async Task TestTooManyTypedGrammarSymbols()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        string str = null;
        IGrammarSymbol<int> symbol = null;
        IGrammarSymbol symbolU = null;

        _ = Production.Build(str, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, str);
        _ = {|#0:Production.Build(str, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, str)|};
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionBuilderFactoryTooManyTypedGrammarSymbols).WithLocation(0).WithArguments("16"));
    }

    [Test]
    public async Task TestUnnecessaryUseEnhancedSyntaxAttribute()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax, {|#0:UseEnhancedSyntax|}]

        _ = Production.Build();

        [{|#1:UseEnhancedSyntax|}]
        static class C
        {
            [UseEnhancedSyntax]
            static void M()
            {
                _ = Production.Build();
            }
        }
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(0),
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(1));
    }

    [Test]
    public async Task TestAttributeOnDifferentPartialParts()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        return;

        [{|#0:UseEnhancedSyntax|}]
        static partial class C;

        static partial class C
        {
            static void M() => {|#1:Production.Build|}();
        }

        static partial class C2
        {
            [{|#2:UseEnhancedSyntax|}]
            static partial void M2();
        }

        static partial class C2
        {
            static partial void M2() => {|#3:Production.Build|}();
        }
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(0),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionBuilderFactoryRequiresEnhancedSyntax).WithLocation(1),
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(2),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionBuilderFactoryRequiresEnhancedSyntax).WithLocation(3));
    }
}
