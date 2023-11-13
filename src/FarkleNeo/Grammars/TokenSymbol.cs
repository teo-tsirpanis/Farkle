// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Represents a token symbol in a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// Token symbols are produced by tokenizers, usually powered by a DFA.
/// </remarks>
/// <seealso cref="Grammar.Terminals"/>
/// <seealso cref="Grammar.TokenSymbols"/>
public readonly struct TokenSymbol
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="TokenSymbol"/>'s <see cref="TokenSymbolHandle"/>.
    /// </summary>
    public TokenSymbolHandle Handle { get; }

    internal TokenSymbol(Grammar grammar, TokenSymbolHandle handle)
    {
        _grammar = grammar;
        Handle = handle;
    }

    /// <summary>
    /// A <see cref="StringHandle"/> pointing to the <see cref="TokenSymbol"/>'s name.
    /// </summary>
    public StringHandle Name
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }
            return _grammar.GrammarTables.GetTokenSymbolName(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    /// <summary>
    /// The token symbol's <see cref="TokenSymbolAttributes"/>.
    /// </summary>
    public TokenSymbolAttributes Attributes
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }
            return _grammar.GrammarTables.GetTokenSymbolFlags(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    internal static string FormatName(string name)
    {
        return ShouldQuote(name) ? $"'{name}'" : name;

        static bool ShouldQuote(string str)
        {
            if (str is "" || !char.IsLetter(str[0]))
            {
                return true;
            }

            foreach (char c in str)
            {
                if (!char.IsLetter(c) && c is not ('.' or '-' or '_'))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Returns a string describing the the <see cref="TokenSymbol"/>.
    /// </summary>
    public override string ToString() => FormatName(_grammar.GetString(Name));
}
