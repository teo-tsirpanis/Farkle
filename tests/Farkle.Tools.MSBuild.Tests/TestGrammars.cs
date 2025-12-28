// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;

namespace Farkle.Tools.MSBuild.Tests;

public static class TestGrammars
{
    [PrecompilerInput]
    public static IGrammarBuilder<double> GrammarBuilderFactory()
    {
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
    public static Grammars.Grammar GrammarFactory() => throw new NotSupportedException();

    [PrecompilerOutput]
    public static CharParser<double> ParserFactory() => CharParser.MustPrecompile<double>();

    [PrecompilerOutput(SyntaxCheck = true)]
    public static CharParser<object?> SyntaxCheckerFactory() => CharParser.MustPrecompile<object?>();

    [PrecompilerOutput(SyntaxCheck = true)]
    public static CharParser<string?> SyntaxCheckerFactory2() => CharParser.MustPrecompile<string?>();
}
