// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Specifies the algorithm that the builder will use to generate the grammar's parsing tables.
/// </summary>
/// <seealso cref="GrammarBuilderExtensions.WithParserGenerationAlgorithm(IGrammarBuilder, ParserGenerationAlgorithm)"/>
/// <seealso cref="GrammarBuilderExtensions.WithParserGenerationAlgorithm{T}(IGrammarBuilder{T}, ParserGenerationAlgorithm)"/>
public enum ParserGenerationAlgorithm
{
    /// <summary>
    /// Generates parsing tables using the LALR(1) algorithm.
    /// </summary>
    Lalr1,

    /// <summary>
    /// Generates parsing tables using the IELR(1) algorithm, described in
    /// <see href="https://www.sciencedirect.com/science/article/pii/S0167642309001191"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This algorithm first attempts to generate tables using the LALR(1) algorithm. If the tables do not contain
    /// conflicts (before considering operator precedence and associativity), IELR(1) will produce identical tables
    /// to LALR(1). If the tables contain conflicts, IELR(1) will attempt to resolve them by adding additional states.
    /// </para>
    /// <para>
    /// IELR(1) is enabled by default and recommended for most use cases.
    /// </para>
    /// </remarks>
    Ielr1,
}
