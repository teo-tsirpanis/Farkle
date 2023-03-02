// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Maps a range of characters to a target DFA state.
/// </summary>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Typically it is <see cref="char"/> or <see cref="byte"/>.</typeparam>
public readonly struct DfaEdge<TChar> : IEquatable<DfaEdge<TChar>>
{
    /// <summary>
    /// The first character in the range, inclusive.
    /// </summary>
    public TChar KeyFrom { get; }

    /// <summary>
    /// The last character in the range, inclusive.
    /// </summary>
    public TChar KeyTo { get; }

    /// <summary>
    /// The index of the target DFA state, starting from 1.
    /// </summary>
    /// <remarks>
    /// A value of 0 indicates that following this edge should stop the tokenizer.
    /// </remarks>
    public int Target { get; }

    /// <summary>
    /// Creates a <see cref="DfaEdge{TChar}"/>.
    /// </summary>
    public DfaEdge(TChar keyFrom, TChar keyTo, int target)
    {
        KeyFrom = keyFrom;
        KeyTo = keyTo;
        Target = target;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DfaEdge<TChar> edge && Equals(edge);

    /// <inheritdoc/>
    public bool Equals(DfaEdge<TChar> other) =>
        EqualityComparer<TChar>.Default.Equals(KeyFrom, other.KeyFrom)
        && EqualityComparer<TChar>.Default.Equals(KeyTo, other.KeyTo)
        && Target == other.Target;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(KeyFrom, KeyTo, Target);

    /// <summary>
    /// Checks two <see cref="DfaEdge{TChar}"/>s for equality.
    /// </summary>
    /// <param name="left">The first edge.</param>
    /// <param name="right">The second edge.</param>
    public static bool operator ==(DfaEdge<TChar> left, DfaEdge<TChar> right) => left.Equals(right);

    /// <summary>
    /// Checks two <see cref="DfaEdge{TChar}"/>s for inequality.
    /// </summary>
    /// <param name="left">The first edge.</param>
    /// <param name="right">The second edge.</param>
    public static bool operator !=(DfaEdge<TChar> left, DfaEdge<TChar> right) => !(left == right);
}
