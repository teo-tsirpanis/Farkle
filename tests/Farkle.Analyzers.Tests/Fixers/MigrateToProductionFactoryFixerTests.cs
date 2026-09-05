// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using VerifyCS = Farkle.Analyzers.Tests.FarkleCodeFixVerify<Farkle.Analyzers.EnhancedSyntax.MigrateToProductionBuilderFactoryAnalyzer, Farkle.Analyzers.EnhancedSyntax.Fixers.MigrateToProductionBuilderFactoryFixer>;

namespace Farkle.Analyzers.Fixers.Tests;

public class MigrateToProductionBuilderFactoryFixerTests
{
    [Test]
    public async Task TestSimpleMigration()
    {
        await VerifyCS.VerifyCodeFixAsync(
            """
            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = [|str.Appended().Extend(symbol).Extend(symbol).Append(symbolU)|];
            """,
            """
            [module: UseEnhancedSyntax]

            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = Production.Build(str, symbol, symbol, symbolU);
            """);
    }

    [Test]
    public async Task TestAttributeAlreadyExists()
    {
        await VerifyCS.VerifyCodeFixAsync(
            """
            [module: UseEnhancedSyntax]

            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = [|str.Appended().Extend(symbol).Extend(symbol).Append(symbolU)|];
            """,
            """
            [module: UseEnhancedSyntax]

            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = Production.Build(str, symbol, symbol, symbolU);
            """);
    }

    [Test]
    public async Task TestCastToIGrammarSymbol()
    {
        await VerifyCS.VerifyCodeFixAsync(
            """
            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = [|str.Appended().Extend(symbol).Append(symbol).Append(symbolU)|];
            """,
            """
            [module: UseEnhancedSyntax]

            string str = null;
            IGrammarSymbol<int> symbol = null;
            IGrammarSymbol symbolU = null;

            _ = Production.Build(str, symbol, (IGrammarSymbol)symbol, symbolU);
            """);
    }
}
