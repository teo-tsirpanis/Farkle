// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains general information about a <see cref="Grammar"/>.
/// </summary>
/// <seealso cref="Grammar.GrammarInfo"/>
[DebuggerDisplay("Name = {Name,nq}; StartSymbol = {StartSymbol}; Attributes = {Attributes}")]
public readonly struct GrammarInfo
{
    private readonly Grammar _grammar { get; }

    internal GrammarInfo(Grammar grammar)
    {
        _grammar = grammar;
    }

    /// <summary>
    /// The grammar's name.
    /// </summary>
    public string Name => _grammar.GetGrammarName();

    /// <summary>
    /// The grammar's starting nonterminal.
    /// </summary>
    public Nonterminal StartSymbol => new(_grammar, _grammar.GrammarTables.GetGrammarStartSymbol(_grammar.GrammarFile));

    /// <summary>
    /// The grammar's <see cref="GrammarAttributes"/>.
    /// </summary>
    public GrammarAttributes Attributes => _grammar.GrammarTables.GetGrammarFlags(_grammar.GrammarFile);
}
