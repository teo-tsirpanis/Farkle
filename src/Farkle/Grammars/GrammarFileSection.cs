// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Points to a region in a grammar file.
/// </summary>
internal readonly struct GrammarFileSection
{
    /// <summary>
    /// The offset to the first byte of the region, relative to the start of the file.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// The length of the region.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Whether the section is empty.
    /// </summary>
    public bool IsEmpty => Length == 0;

    public static GrammarFileSection Empty => default;

    public GrammarFileSection(int offset, int length)
    {
        Debug.Assert(offset >= 0);
        Debug.Assert(length >= 0);
        Offset = offset;
        Length = length;
    }
}
