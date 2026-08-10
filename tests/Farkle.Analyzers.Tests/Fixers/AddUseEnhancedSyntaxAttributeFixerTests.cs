// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Analyzers.EnhancedSyntax.Fixers;
using TestCS = Farkle.Analyzers.Tests.FarkleCodeFixTest<Farkle.Analyzers.EnhancedSyntax.ProductionFactoryAnalyzer, Farkle.Analyzers.EnhancedSyntax.Fixers.AddUseEnhancedSyntaxAttributeFixer>;

namespace Farkle.Analyzers.Fixers.Tests;

public class AddUseEnhancedSyntaxAttributeFixerTests
{
    [Test]
    public async Task TestAddAttributeOnMember()
    {
        var test = new TestCS
        {
            TestCode = """
            return;

            public class MyGrammar
            {
                public ProductionBuilder MyProduction => {|FARKLE1005:Production.Create|}("a");

                public ProductionBuilder MyProduction2 => {|FARKLE1005:Production.Create|}("a");
            }
            """,
            FixedCode = """
            return;

            public class MyGrammar
            {
                [UseEnhancedSyntax]
                public ProductionBuilder MyProduction => Production.Create("a");

                [UseEnhancedSyntax]
                public ProductionBuilder MyProduction2 => Production.Create("a");
            }
            """,
            CodeActionEquivalenceKey = AddUseEnhancedSyntaxAttributeFixer.AddOnDeclaringMemberKey,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task TestAddAttributeOnType()
    {
        var test = new TestCS
        {
            TestCode = """
            return;

            public class MyGrammar
            {
                public ProductionBuilder MyProduction => {|FARKLE1005:Production.Create|}("a");

                public ProductionBuilder MyProduction2 => {|FARKLE1005:Production.Create|}("a");
            }
            """,
            FixedCode = """
            return;

            [UseEnhancedSyntax]
            public class MyGrammar
            {
                public ProductionBuilder MyProduction => Production.Create("a");

                public ProductionBuilder MyProduction2 => Production.Create("a");
            }
            """,
            CodeActionEquivalenceKey = AddUseEnhancedSyntaxAttributeFixer.AddOnDeclaringTypeKey,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task TestAddAttributeOnModule()
    {
        var test = new TestCS
        {
            TestCode = """
            _ = {|FARKLE1005:Production.Create|}("a");
            """,
            FixedCode = """
            [module: UseEnhancedSyntax]

            _ = Production.Create("a");
            """,
            CodeActionEquivalenceKey = AddUseEnhancedSyntaxAttributeFixer.AddOnDeclaringMemberKey,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task TestXmlDocumentation()
    {
        var test = new TestCS
        {
            TestCode = """
            return;

            /// <summary>
            /// My grammar class.
            /// </summary>
            public class MyGrammar
            {
                public ProductionBuilder MyProduction => {|FARKLE1005:Production.Create|}("a");
            }
            
            /// <summary>
            /// My grammar class 2.
            /// </summary>
            [System.Obsolete]
            public class MyGrammar2
            {
                public ProductionBuilder MyProduction => {|FARKLE1005:Production.Create|}("a");
            }
            """,
            FixedCode = """
            return;
            
            /// <summary>
            /// My grammar class.
            /// </summary>
            [UseEnhancedSyntax]
            public class MyGrammar
            {
                public ProductionBuilder MyProduction => Production.Create("a");
            }
            
            /// <summary>
            /// My grammar class 2.
            /// </summary>
            [System.Obsolete]
            [UseEnhancedSyntax]
            public class MyGrammar2
            {
                public ProductionBuilder MyProduction => Production.Create("a");
            }
            """,
            CodeActionEquivalenceKey = AddUseEnhancedSyntaxAttributeFixer.AddOnDeclaringTypeKey,
        };

        await test.RunAsync();
    }

}
