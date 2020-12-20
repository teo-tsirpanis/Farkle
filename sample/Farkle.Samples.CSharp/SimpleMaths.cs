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
        public static readonly DesigntimeFarkle<double> Designtime;
        public static readonly RuntimeFarkle<double> Runtime;

        static SimpleMaths()
        {
            var number = Terminals.Double("Number");

            var expression = Nonterminal.Create<double>("Expression");
            expression.SetProductions(
                number.AsIs(),
                expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
                expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
                expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
                expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
                "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
                expression.Extended().Append("^").Extend(expression).Finish(Math.Pow),
                "(".Appended().Extend(expression).Append(")").AsIs());

            var opScope = new OperatorScope(
                new LeftAssociative("+", "-"),
                new LeftAssociative("*", "/"),
                new PrecedenceOnly(NEG),
                new RightAssociative("^"));

            Designtime = expression.WithOperatorScope(opScope);
            Runtime = Designtime.Build();
        }
    }
}
