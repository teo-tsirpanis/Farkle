// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a token symbol in a <see cref="Grammars.Grammar"/>.
/// </summary>
/// <remarks>
/// Token symbols are produced by tokenizers, usually powered by a DFA.
/// </remarks>
/// <seealso cref="Grammar.Terminals"/>
/// <seealso cref="Grammar.TokenSymbols"/>
public readonly struct TokenSymbolDefinition : IEquatable<TokenSymbolDefinition>
{
    internal Grammar Grammar { get; }

    /// <summary>
    /// The <see cref="TokenSymbolDefinition"/>'s <see cref="TokenSymbolHandle"/>.
    /// </summary>
    public TokenSymbolHandle Handle { get; }

    internal TokenSymbolDefinition(Grammar grammar, TokenSymbolHandle handle)
    {
        Grammar = grammar;
        Handle = handle;
    }

    /// <summary>
    /// The <see cref="TokenSymbolDefinition"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }
            return Grammar.GetTokenSymbolName(Handle);
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
            return Grammar.GrammarTables.GetTokenSymbolFlags(Grammar.GrammarFile, Handle.TableIndex);
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

    /// <inheritdoc/>
    public bool Equals(TokenSymbolDefinition other) => Grammar == other.Grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TokenSymbolDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="TokenSymbolDefinition"/>.
    /// </summary>
    public override string ToString() => Grammar is null ? "" : FormatName(Name);

    /// <summary>
    /// Compares two <see cref="TokenSymbolDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first token symbol.</param>
    /// <param name="right">The second token symbol.</param>
    public static bool operator ==(TokenSymbolDefinition left, TokenSymbolDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="TokenSymbolDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first token symbol.</param>
    /// <param name="right">The second token symbol.</param>
    public static bool operator !=(TokenSymbolDefinition left, TokenSymbolDefinition right) => !left.Equals(right);
}
