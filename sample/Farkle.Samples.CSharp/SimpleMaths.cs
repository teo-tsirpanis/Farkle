// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using System;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;

namespace Farkle.Samples.CSharp
{
    public static class SimpleMaths
    {
        public static readonly IGrammarBuilder<double> Builder;
        public static readonly CharParser<double> Parser;

        static SimpleMaths()
        {
            var number = Terminals.Double("Number");

            var expression = Nonterminal.Create<double>("Expression");
            expression.SetProductions(
                number.AsProduction(),
                expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
                expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
                expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
                expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
                "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
                expression.Extended().Append("^").Extend(expression).Finish(Math.Pow),
                "(".Appended().Extend(expression).Append(")").AsProduction());

            Builder = expression.WithOperatorScope(
                new LeftAssociative("+", "-"),
                new LeftAssociative("*", "/"),
                new PrecedenceOnly(NEG),
                new RightAssociative("^"));
            Parser = Builder.Build();
        }
    }
}
