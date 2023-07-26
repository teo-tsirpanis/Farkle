// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Exposes a <see cref="Grammar"/> and enables performing certain
/// grammar operations without materializing a <see cref="Grammar"/>
/// instance.
/// </summary>
/// <remarks>
/// This interface is trivially implemented by <see cref="Grammar"/>.
/// Its purpose is to allow trimming the grammar binary blob and reader
/// code if only a subset of the grammar is needed, but this is not
/// implemented in this version of Farkle.
/// </remarks>
public interface IGrammarProvider
{
    /// <summary>
    /// Gets the <see cref="Grammar"/> this <see cref="IGrammarProvider"/> holds.
    /// </summary>
    Grammar GetGrammar();

    /// <inheritdoc cref="Grammar.GetSymbolFromSpecialName"/>
    EntityHandle GetSymbolFromSpecialName(string specialName, bool throwIfNotFound = false);
}
