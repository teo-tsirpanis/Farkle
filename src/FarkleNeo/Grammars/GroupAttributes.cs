// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Characteristics of a group.
/// </summary>
[Flags]
public enum GroupAttributes : ushort
{
    /// <summary>
    /// No attributes are defined.
    /// </summary>
    None = 0,
    /// <summary>
    /// The group has a non-empty nesting list.
    /// </summary>
    HasNesting = 1 << 0,
    /// <summary>
    /// The group can also end when the end of input is reached, without encountering its end symbol.
    /// </summary>
    EndsOnEndOfInput = 1 << 1,
    /// <summary>
    /// When inside the group, the parser should read the input without invoking the regular tokenizer.
    /// </summary>
    AdvanceByCharacter = 1 << 2,
    /// <summary>
    /// When the group ends, the parser should keep the token that ended the group in the input stream.
    /// </summary>
    KeepEndToken = 1 << 3
}
