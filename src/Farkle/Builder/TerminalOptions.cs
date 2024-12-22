// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Contains options to customize the creation of terminals.
/// </summary>
[Flags]
public enum TerminalOptions
{
    /// <summary>
    /// No options are specified.
    /// </summary>
    None = 0,
    /// <summary>
    /// The terminal will be ignored if it is encountered in the input in an unexpected place.
    /// </summary>
    /// <seealso cref="TokenSymbolAttributes.Noise"/>
    Noisy = 1,
    /// <summary>
    /// The terminal will not be shown in the list of expected symbols in case of a parse error.
    /// </summary>
    /// <seealso cref="TokenSymbolAttributes.Hidden"/>
    Hidden = 2,
    /// <summary>
    /// The terminal will be added to the grammar's special names table under its original name.
    /// </summary>
    /// <remarks>
    /// Because special names in a grammar are unique, if many symbols in a grammar have the same
    /// special name in the grammar, the builder will create a grammar unusable for parsing. For
    /// this reason you are recommended to use a special name that is likely to be unique, and
    /// rename the symbol to a more user-friendly name.
    /// </remarks>
    /// <seealso cref="IGrammarProvider.GetSymbolFromSpecialName"/>
    SpecialName = 4
}
