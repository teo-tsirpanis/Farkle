// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Provides information about the token symbols of a grammar to be built.
/// </summary>
internal interface IGrammarSymbolsProvider
{
    /// <summary>
    /// The number of token symbols in the grammar.
    /// </summary>
    int SymbolCount { get; }
    /// <summary>
    /// Gets the <see cref="Regex"/> of the token symbol at the specified index.
    /// </summary>
    /// <param name="index">The index of the token symbol.</param>
    Regex GetRegex(int index);
    /// <summary>
    /// Gets the <see cref="TokenSymbolHandle"/> of the token symbol at the specified index.
    /// </summary>
    /// <param name="index">The index of the token symbol.</param>
    TokenSymbolHandle GetTokenSymbolHandle(int index);
    /// <summary>
    /// Gets the <see cref="BuilderSymbolName"/> of the token symbol at the specified index.
    /// </summary>
    /// <param name="index">The index of the token symbol.</param>
    BuilderSymbolName GetName(int index);
}
