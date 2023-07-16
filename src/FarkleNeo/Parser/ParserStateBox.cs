// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Implements <see cref="IParserStateBox"/> trivially.
/// </summary>
/// <remarks>
/// This class is intended to be used on frameworks that do not support ref fields.
/// On frameworks that do support them, it is recommended to just declare a
/// <see cref="ParserState"/> variable and pass it to <see cref="ParserInputReader{TChar}"/>
/// by reference.
/// </remarks>
#if NET7_0_OR_GREATER
[Obsolete("This type is provided for compatibility with frameworks that do not support ref fields. " +
    "In .NET 7+ use a ParserState and pass it to the ParserInputReader's constructor by reference instead.")]
#endif
public sealed class ParserStateBox : IParserStateBox
{
    private ParserState _state;

    /// <summary>
    /// Creates a <see cref="ParserStateBox"/>.
    /// </summary>
    public ParserStateBox() {}

    /// <inheritdoc/>
    public ref ParserState State => ref _state;
}
