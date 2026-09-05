// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Analyzers.Models;

public readonly record struct ProductionBuilderFactoryInvocation(EquatableArray<ProductionMemberType> MemberTypes) : IComparable<ProductionBuilderFactoryInvocation>
{
    public int TypeArity => MemberTypes.Count(x => x == ProductionMemberType.IGrammarSymbol);

    public int CompareTo(ProductionBuilderFactoryInvocation other)
    {
        var arityComparison = TypeArity.CompareTo(other.TypeArity);
        if (arityComparison != 0)
        {
            return arityComparison;
        }

        var lengthComparison = MemberTypes.Count.CompareTo(other.MemberTypes.Count);
        if (lengthComparison != 0)
        {
            return lengthComparison;
        }

        for (var i = 0; i < MemberTypes.Count; i++)
        {
            var typeComparison = MemberTypes[i].CompareTo(other.MemberTypes[i]);
            if (typeComparison != 0)
            {
                return typeComparison;
            }
        }

        return 0;
    }
}
