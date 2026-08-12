// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;

namespace Farkle.Samples.CSharp
{
    public static class SimpleMaths
    {
        public static readonly IGrammarBuilder<double> Builder;
        public static readonly CharParser<double> Parser;

        [UseEnhancedSyntax]
        static SimpleMaths()
        {
            var number = Terminals.Double("Number");

            var expression = Nonterminal.Create<double>("Expression");
            expression.SetProductions(
                number.AsProduction(),
                Production.Create(expression, "+", expression).Finish((x1, x2) => x1 + x2),
                Production.Create(expression, "-", expression).Finish((x1, x2) => x1 - x2),
                Production.Create(expression, "*", expression).Finish((x1, x2) => x1 * x2),
                Production.Create(expression, "/", expression).Finish((x1, x2) => x1 / x2),
                Production.Create("-", expression).WithPrecedence(out var NEG).Finish(x => -x),
                Production.Create(expression, "^", expression).Finish(Math.Pow),
                Production.Create("(", expression, ")").AsProduction());

            Builder = expression.WithOperatorScope([
                new LeftAssociative("+", "-"),
                new LeftAssociative("*", "/"),
                new PrecedenceOnly(NEG),
                new RightAssociative("^")]);
            Parser = Builder.Build();
        }
    }
}
