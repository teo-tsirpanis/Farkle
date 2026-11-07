// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

internal readonly struct ParserOptions : IEquatable<ParserOptions>
{
    public bool EnableLookaheadCorrection { get; init; } = false;

    public ParserOptions() { }

    public bool Equals(ParserOptions other) => EnableLookaheadCorrection == other.EnableLookaheadCorrection;

    public override bool Equals(object? obj) => obj is ParserOptions other && Equals(other);

    public override int GetHashCode() => EnableLookaheadCorrection.GetHashCode();
}
