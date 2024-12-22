// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Provides a reference to a <see cref="ParserState"/>.
/// </summary>
/// <remarks>
/// This interface is intended to be used to construct <see cref="ParserInputReader{TChar}"/>
/// values on frameworks that do not support ref fields. It is implemented by
/// <see cref="ParserStateBox"/> and <see cref="ParserStateContext{TChar}"/>.
/// </remarks>
public interface IParserStateBox
{
    /// <summary>
    /// A reference to a <see cref="ParserState"/>.
    /// </summary>
    ref ParserState State { get; }
}
