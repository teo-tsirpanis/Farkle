// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Contains general information about a <see cref="Grammar"/>.
/// </summary>
/// <seealso cref="Grammar.GrammarInfo"/>
public readonly struct GrammarInfo
{
    private readonly Grammar _grammar { get; }

    internal GrammarInfo(Grammar grammar)
    {
        _grammar = grammar;
    }

    /// <summary>
    /// A <see cref="StringHandle"/> pointing to the grammar's name.
    /// </summary>
    public StringHandle Name => _grammar.GrammarTables.GetGrammarName(_grammar.GrammarFile);

    /// <summary>
    /// The grammar's starting nonterminal.
    /// </summary>
    public NonterminalHandle StartSymbol => _grammar.GrammarTables.GetGrammarStartSymbol(_grammar.GrammarFile);

    /// <summary>
    /// The grammar's <see cref="GrammarAttributes"/>.
    /// </summary>
    public GrammarAttributes Attributes => _grammar.GrammarTables.GetGrammarFlags(_grammar.GrammarFile);
}
