// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using VerifyCS = Farkle.Analyzers.Tests.FarkleCodeFixVerify<Farkle.Analyzers.EnhancedSyntax.ProductionBuilderFactoryAnalyzer, Farkle.Analyzers.EnhancedSyntax.Fixers.RemoveUnnecessaryAttributeFixer>;

namespace Farkle.Analyzers.Fixers.Tests;

public class RemoveUnnecessaryAttributeFixerTests
{
    [Test]
    public async Task TestEmpty()
    {
        await VerifyCS.VerifyCodeFixAsync(
            """
            [module: {|FARKLE1007:UseEnhancedSyntax|}, {|FARKLE1007:UseEnhancedSyntax|}]
            [module: {|FARKLE1007:UseEnhancedSyntax|}]

            return;
            """,
            """

            return;
            """);
    }

    [Test]
    public async Task TestAttributeAlreadyExists()
    {
        await VerifyCS.VerifyCodeFixAsync(
            """
            [module: {|FARKLE1007:UseEnhancedSyntax|}]
            return;

            [UseEnhancedSyntax]
            void f()
            {
                _ = Production.Build("a");
            }
            """,
            """
            return;

            [UseEnhancedSyntax]
            void f()
            {
                _ = Production.Build("a");
            }
            """);
    }
}
