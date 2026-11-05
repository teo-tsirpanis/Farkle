// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

internal readonly struct ParserOptions : IEquatable<ParserOptions>
{
    public LookaheadCorrectionOption LookaheadCorrection { get; init; } = LookaheadCorrectionOption.OnSyntaxErrors;

    public ParserOptions() { }

    public bool Equals(ParserOptions other) => LookaheadCorrection == other.LookaheadCorrection;

    public override bool Equals(object? obj) => obj is ParserOptions other && Equals(other);

    public override int GetHashCode() => LookaheadCorrection.GetHashCode();
}
