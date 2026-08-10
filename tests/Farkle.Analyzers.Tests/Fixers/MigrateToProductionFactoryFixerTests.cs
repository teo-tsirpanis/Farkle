// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using VerifyCS = Farkle.Analyzers.Tests.FarkleCodeFixVerify<Farkle.Analyzers.EnhancedSyntax.MigrateToProductionFactoryAnalyzer, Farkle.Analyzers.EnhancedSyntax.Fixers.MigrateToProductionFactoryFixer>;

namespace Farkle.Analyzers.Fixers.Tests;

public class MigrateToProductionFactoryFixerTests
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

            _ = Production.Create(str, symbol, symbol, symbolU);
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

            _ = Production.Create(str, symbol, symbol, symbolU);
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

            _ = Production.Create(str, symbol, (IGrammarSymbol)symbol, symbolU);
            """);
    }
}
