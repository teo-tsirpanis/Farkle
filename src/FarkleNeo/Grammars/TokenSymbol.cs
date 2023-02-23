// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

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
    /// A <see cref="StringHandle"/> to the <see cref="TokenSymbol"/>'s name.
    /// </summary>
    public StringHandle NameHandle
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
    /// The <see cref="TokenSymbol"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }
            ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;
            StringHandle handle = _grammar.GrammarTables.GetTokenSymbolName(grammarFile, Handle.TableIndex);
            return _grammar.StringHeap.GetString(grammarFile, handle);
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

    /// <summary>
    /// Returns the <see cref="TokenSymbol"/>'s <see cref="Name"/>.
    /// </summary>
    public override string ToString() => Name;
}
