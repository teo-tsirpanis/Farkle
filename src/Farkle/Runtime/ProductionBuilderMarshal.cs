// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;

namespace Farkle.Runtime;

/// <summary>
/// Contains low-level APIs to create production builder objects.
/// </summary>
public static class ProductionBuilderMarshal
{
    /// <summary>
    /// Creates a production builder with a full list of members.
    /// </summary>
    /// <param name="members">The production's members.</param>
    /// <param name="significantMemberIndices">The indices to <paramref name="members"/>,
    /// corresponding to the positions of the production's significant members.</param>
    /// <remarks>
    /// <para>
    /// Creating production builders from this method is more efficient than using the fluent API.
    /// Most users don't need to use this method directly, as the enhanced syntax source generator
    /// will generate compatible overloads of the <c>Production.Build</c> method for each symbol
    /// type combination.
    /// </para>
    /// <para>
    /// The symbols in <paramref name="members"/> that are pointed to by <paramref name="significantMemberIndices"/>
    /// must implement <see cref="IGrammarSymbol{T}"/> for some type <c>T</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="significantMemberIndices"/> is not
    /// equal to the expected number of significant members, or any such index is negative or greater than or equal to
    /// the length of <paramref name="members"/>.</exception>
    public static T Create<T>(ReadOnlySpan<IGrammarSymbol> members, ReadOnlySpan<int> significantMemberIndices) where T : IProductionBuilder<T>
    {
        return T.Create(members, significantMemberIndices);
    }
}
