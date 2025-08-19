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
}
