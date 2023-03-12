// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Characteristics of a <see cref="Grammar"/>.
/// </summary>
[Flags]
public enum GrammarAttributes : ushort
{
    /// <summary>
    /// No attributes are defined.
    /// </summary>
    None = 0,
    /// <summary>
    /// The grammar cannot be used for parsing, even if the necessary state machines are present.
    /// </summary>
    Unparsable = 1 << 0,
    /// <summary>
    /// The grammar cannot be used for parsing if it contains data unknown to its reader.
    /// </summary>
    /// <seealso cref="Grammar.HasUnknownData"/>
    Critical = 1 << 1
}
