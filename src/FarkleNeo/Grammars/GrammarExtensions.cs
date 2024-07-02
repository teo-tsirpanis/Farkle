// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Provides extension methods on <see cref="Grammar"/> and <see cref="IGrammarProvider"/>.
/// </summary>
public static class GrammarExtensions
{
    /// <summary>
    /// Looks up a token symbol with the specified special name.
    /// </summary>
    /// <param name="grammarProvider">The grammar provider.</param>
    /// <param name="specialName">The symbol's special name.</param>
    /// <param name="throwIfNotFound">Whether to throw an exception if the symbol was not found.
    /// Defaults to <see true="false"/>.</param>
    /// <returns>A <see cref="TokenSymbolHandle"/> pointing to the token symbol with the specified
    /// special name, or pointing to nothing if the symbol was not found and
    /// <paramref name="throwIfNotFound"/> has a value of <see langword="false"/>.</returns>
    /// <remarks>
    /// Special names are intended to be used on token symbols that will be emitted by custom
    /// tokenizers. Because symbol names are not guaranteed to be unique, a special name
    /// provides a guaranteed way to retrieve the handle for a specific symbol.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The symbol was not found or is not a token symbol,
    /// and <paramref name="throwIfNotFound"/> had a value of <see langword="true"/>.</exception>
    public static TokenSymbolHandle GetTokenSymbolFromSpecialName(this IGrammarProvider grammarProvider, string specialName, bool throwIfNotFound = true)
    {
        EntityHandle handle = grammarProvider.GetSymbolFromSpecialName(specialName, throwIfNotFound);

        if (handle.IsTokenSymbol)
        {
            return (TokenSymbolHandle)handle;
        }

        if (throwIfNotFound)
        {
            ThrowHelpers.ThrowSpecialNameNotFound(specialName);
        }

        return default;
    }

    /// <summary>
    /// Looks up a nonterminal with the specified special name.
    /// </summary>
    /// <param name="grammarProvider">The grammar provider.</param>
    /// <param name="specialName">The symbol's special name.</param>
    /// <param name="throwIfNotFound">Whether to throw an exception if the symbol was not found.
    /// Defaults to <see true="false"/>.</param>
    /// <returns>A <see cref="TokenSymbolHandle"/> pointing to the nonterminal with the specified
    /// special name, or pointing to nothing if the symbol was not found and
    /// <paramref name="throwIfNotFound"/> has a value of <see langword="false"/>.</returns>
    /// <remarks>
    /// Special names are intended to be used on nonterminals that will be emitted by custom
    /// tokenizers. Because symbol names are not guaranteed to be unique, a special name
    /// provides a guaranteed way to retrieve the handle for a specific symbol.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The symbol was not found or is not a nonterminal,
    /// and <paramref name="throwIfNotFound"/> had a value of <see langword="true"/>.</exception>
    public static NonterminalHandle GetNonterminalFromSpecialName(this IGrammarProvider grammarProvider, string specialName, bool throwIfNotFound = true)
    {
        EntityHandle handle = grammarProvider.GetSymbolFromSpecialName(specialName, throwIfNotFound);

        if (handle.IsNonterminal)
        {
            return (NonterminalHandle)handle;
        }

        if (throwIfNotFound)
        {
            ThrowHelpers.ThrowSpecialNameNotFound(specialName);
        }

        return default;
    }

    /// <summary>
    /// Writes the binary data of a <see cref="Grammar"/> to a file.
    /// </summary>
    /// <param name="grammar">The grammar.</param>
    /// <param name="path">The path to write the grammar's data to.</param>
    public static void WriteGrammarToFile(this Grammar grammar, string path)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(path);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        stream.Write(grammar.Data);
#else
        var array = grammar.Data.ToArray();
        stream.Write(array, 0, array.Length);
#endif
    }
}
