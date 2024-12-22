// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Provides options to configure a <see cref="ParserStateContext{TChar}"/>.
/// </summary>
public sealed class ParserStateContextOptions
{
    internal const int DefaultInitialBufferSize = 512;

    /// <summary>
    /// The initial size of the buffer used to store the input characters.
    /// </summary>
    /// <remarks>
    /// Currently defaults to 512 characters. This value may change in the future.
    /// </remarks>
    public int InitialBufferSize { get; init; } = DefaultInitialBufferSize;
}
