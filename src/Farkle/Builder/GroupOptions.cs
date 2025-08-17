// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Contains options to customize the creation of groups.
/// </summary>
[Flags]
public enum GroupOptions
{
    /// <summary>
    /// No options are specified.
    /// </summary>
    None = 0,
    /// <summary>
    /// The group will be ignored if it is encountered in the input in an unexpected place.
    /// </summary>
    /// <seealso cref="TokenSymbolAttributes.Noise"/>
    Noisy = 1,
    /// <summary>
    /// The group will not be shown in the list of expected symbols in case of a parse error.
    /// </summary>
    /// <seealso cref="TokenSymbolAttributes.Hidden"/>
    Hidden = 2,
    /// <summary>
    /// The group can appear inside itself. For each time the group starts, it must end
    /// an equal number of times.
    /// </summary>
    /// <remarks>
    /// Semantic actions do not run for nested groups.
    /// </remarks>
    /// <seealso cref="Grammars.Group.Nesting"/>
    Recursive = 4
}
