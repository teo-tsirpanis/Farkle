// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

using Farkle.Runtime;
using Farkle.Builder.ProductionBuilders;

internal static partial class ProductionFactory
{
    public static ProductionBuilder Create(IGrammarSymbol member1) =>
        ProductionBuilderMarshal.Create<ProductionBuilder>([member1], []);

    public static ProductionBuilder Create(string member1) =>
        ProductionBuilderMarshal.Create<ProductionBuilder>([Terminal.Literal(member1)], []);

    public static ProductionBuilder<T1> Create<T1>(IGrammarSymbol<T1> member1) =>
        ProductionBuilderMarshal.Create<ProductionBuilder<T1>>([member1], [0]);

    public static ProductionBuilder<T1> Create<T1>(string member1, IGrammarSymbol<T1> member2, string member3) =>
        ProductionBuilderMarshal.Create<ProductionBuilder<T1>>([Terminal.Literal(member1), member2, Terminal.Literal(member3)], [1]);

    public static ProductionBuilder<T1, T2> Create<T1, T2>(IGrammarSymbol<T1> member1, IGrammarSymbol<T2> member2) =>
        ProductionBuilderMarshal.Create<ProductionBuilder<T1, T2>>([member1, member2], [0, 1]);

    public static ProductionBuilder<T1, T2> Create<T1, T2>(IGrammarSymbol<T1> member1, IGrammarSymbol member2, IGrammarSymbol<T2> member3) =>
        ProductionBuilderMarshal.Create<ProductionBuilder<T1, T2>>([member1, member2, member3], [0, 2]);

}
