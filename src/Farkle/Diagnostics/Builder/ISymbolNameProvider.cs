// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Diagnostics.Builder;

internal interface ISymbolNameProvider
{
    /// <summary>
    /// Gets the name of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    BuilderSymbolName GetName(TokenSymbolHandle symbol);
}
