// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Grammars;

namespace Farkle.Tools.MSBuild.Tests;

public static class TestGrammars
{
    [PrecompilerInput]
    public static IGrammarBuilder<double> GrammarBuilderFactory()
    {
        // Force resolving a dependency from a package with ref assemblies during precompilation.
        GC.KeepAlive(Microsoft.Data.SqlClient.SqlClientFactory.Instance);

        var expr = Nonterminal.Create<double>("Expression");
        expr.SetProductions(
            Terminals.UnsignedInteger<double>("Integer").AsProduction(),
            Terminals.UnsignedFloat<double>("Number").AsProduction(),
            expr.Extended().Append("+").Extend(expr).Finish((x1, x2) => x1 + x2),
            expr.Extended().Append("-").Extend(expr).Finish((x1, x2) => x1 - x2),
            expr.Extended().Append("*").Extend(expr).Finish((x1, x2) => x1 * x2),
            expr.Extended().Append("/").Extend(expr).Finish((x1, x2) => x1 / x2),
            "(".Appended().Extend(expr).Append(")").AsProduction()
        );
        var opScope = new OperatorScope(
            new LeftAssociative("+", "-"),
            new LeftAssociative("*", "/")
        );
        return expr
            .WithGrammarName("Maths")
            .WithOperatorScope(opScope);
    }

    [PrecompilerOutput]
    public static Grammar GrammarFactory() => throw new NotSupportedException();

    [PrecompilerOutput]
    public static CharParser<double> ParserFactory() => CharParser.MustPrecompile<double>();

    [PrecompilerOutput(SyntaxCheck = true)]
    public static CharParser<object?> SyntaxCheckerFactory() => CharParser.MustPrecompile<object?>();

    [PrecompilerOutput(SyntaxCheck = true)]
    public static CharParser<string?> SyntaxCheckerFactory2() => CharParser.MustPrecompile<string?>();
}
