// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Text;

namespace Farkle.Grammars;

/// <summary>
/// A descendant of <see cref="UTF8Encoding"/> that throws when an invalid encoding is detected.
/// </summary>
internal sealed class Utf8EncodingStrict : UTF8Encoding
{
    public static readonly Utf8EncodingStrict Instance = new();

    private Utf8EncodingStrict() : base(true, true) { }
}
