// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using VerifyCS = Farkle.Analyzers.Tests.FarkleAnalyzerVerifier<Farkle.Analyzers.EnhancedSyntax.ProductionFactoryAnalyzer>;

namespace Farkle.Analyzers.Tests;

public class ProductionFactoryAnalyzerTests
{
    [Test]
    public async Task TestHappyPath()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        string str = null;
        IGrammarSymbol<int> symbol = null;
        IGrammarSymbol symbolU = null;

        _ = Production.Create(str, symbol, symbolU);
        _ = Production.Create(str, str, str);
        _ = Production.Create(symbol, symbol, symbol);
        _ = Production.Create(symbol, symbolU, symbolU);
        """);
    }

    [Test]
    public async Task TestImplicitlyConvertibleToString()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        _ = Production.Create(default(MyString));

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

        _ = Production.Create({|#0:i|});
        _ = Production.Create({|#1:builder|});
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionFactoryUnsupportedType).WithLocation(0).WithArguments("0", "int"),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionFactoryUnsupportedType).WithLocation(1).WithArguments("0", "Farkle.Builder.IGrammarBuilder"));
    }

    [Test]
    public async Task TestTooManyTypedGrammarSymbols()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax]

        string str = null;
        IGrammarSymbol<int> symbol = null;
        IGrammarSymbol symbolU = null;

        _ = Production.Create(str, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, symbolU, str);
        _ = {|#0:Production.Create(str, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, symbol, str)|};
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionFactoryTooManyTypedGrammarSymbols).WithLocation(0).WithArguments("16"));
    }

    [Test]
    public async Task TestUnnecessaryUseEnhancedSyntaxAttribute()
    {
        await VerifyCS.VerifyAnalyzerAsync("""
        [module: UseEnhancedSyntax, {|#0:UseEnhancedSyntax|}]

        _ = Production.Create();

        [{|#1:UseEnhancedSyntax|}]
        static class C
        {
            [UseEnhancedSyntax]
            static void M()
            {
                _ = Production.Create();
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
            static void M() => {|#1:Production.Create()|};
        }

        static partial class C2
        {
            [{|#2:UseEnhancedSyntax|}]
            static partial void M2();
        }

        static partial class C2
        {
            static partial void M2() => {|#3:Production.Create()|};
        }
        """,
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(0),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax).WithLocation(1),
        VerifyCS.Diagnostic(DiagnosticDescriptors.UseEnhancedSyntaxAttributeUnnecessary).WithLocation(2),
        VerifyCS.Diagnostic(DiagnosticDescriptors.ProductionFactoryRequiresEnhancedSyntax).WithLocation(3));
    }
}
