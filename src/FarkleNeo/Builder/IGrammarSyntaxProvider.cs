// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides information about the productions of a grammar to be built.
/// </summary>
internal interface IGrammarSyntaxProvider
{
    /// <summary>
    /// The number of terminals in the grammar.
    /// </summary>
    /// <remarks>
    /// Terminals are indexed by consecutive integers starting from
    /// zero to the value of this property minus one.
    /// </remarks>
    int TerminalCount { get; }

    /// <summary>
    /// The number of nonterminals in the grammar.
    /// </summary>
    /// <remarks>
    /// Nonterminals are indexed by consecutive integers starting from
    /// zero to the value of this property minus one.
    /// </remarks>
    int NonterminalCount { get; }

    /// <summary>
    /// The number of productions in the grammar.
    /// </summary>
    /// <remarks>
    /// Productions are indexed by consecutive integers starting from
    /// zero to the value of this property minus one.
    /// </remarks>
    int ProductionCount { get; }

    /// <summary>
    /// The index of the starting nonterminal of the grammar.
    /// </summary>
    int StartSymbol { get; }

    /// <summary>
    /// Gets the name of a terminal.
    /// </summary>
    /// <param name="index">The index of the terminal.</param>
    /// <returns>
    /// The terminal's name, without any quoting or formatting.
    /// </returns>
    string GetTerminalName(int index);

    /// <summary>
    /// Gets the name of a nonterminal.
    /// </summary>
    /// <param name="index">The index of the nonterminal.</param>
    /// <returns>
    /// The nonterminal's name, without any quoting or formatting.
    /// </returns>
    string GetNonterminalName(int index);

    /// <summary>
    /// Gets the indices of the productions that have a certain nonterminal at
    /// their left-hand side.
    /// </summary>
    /// <param name="index">The index of the nonterminal.</param>
    /// <returns>A tuple with the index of the first production of the nonterminal,
    /// and the number of productions.</returns>
    (int FirstProduction, int ProductionCount) GetNonterminalProductions(int index);

    /// <summary>
    /// Gets the index of the nonterminal at the left-hand side of a production.
    /// </summary>
    /// <param name="index">The index of the production.</param>
    int GetProductionHead(int index);

    /// <summary>
    /// Gets the number of members in a production.
    /// </summary>
    /// <param name="index">The index of the production.</param>
    /// <returns>A tuple with the index of the first member of the production,
    /// and the number of members.</returns>
    /// <seealso cref="GetNonterminalProductions"/>
    (int FirstMember, int MemberCount) GetProductionMembers(int index);

    /// <summary>
    /// Gets a member of a production.
    /// </summary>
    /// <param name="index">The index of the symbol within the production.</param>
    /// <returns>A tuple with the symbol's index, and whether the symbol is a terminal or a nonterminal.</returns>
    /// <remarks>
    /// Production members are indexed globally across all productions in the grammar. Use
    /// <see cref="GetProductionMembers"/> to get the bounds of the members of a specific production.
    /// </remarks>
    /// <seealso cref="GetProductionMembers"/>
    (int SymbolIndex, bool IsTerminal) GetProductionMember(int index);
}
