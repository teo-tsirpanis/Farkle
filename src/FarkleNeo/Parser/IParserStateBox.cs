// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Provides a reference to a <see cref="ParserState"/>.
/// </summary>
public interface IParserStateBox
{
    /// <summary>
    /// A reference to a <see cref="ParserState"/>.
    /// </summary>
    ref ParserState State { get; }
}
