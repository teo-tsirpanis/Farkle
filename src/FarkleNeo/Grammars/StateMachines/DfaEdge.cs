// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Maps a range of characters to a target DFA state.
/// </summary>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Typically it is <see cref="char"/> or <see cref="byte"/>.</typeparam>
/// <param name="keyFrom">The value of <see cref="KeyFrom"/></param>
/// <param name="keyTo">The value of <see cref="KeyTo"/></param>
/// <param name="target">The value of <see cref="Target"/></param>
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
public readonly struct DfaEdge<TChar>(TChar keyFrom, TChar keyTo, int target) : IEquatable<DfaEdge<TChar>>
{
    /// <summary>
    /// The first character in the range, inclusive.
    /// </summary>
    public TChar KeyFrom { get; } = keyFrom;

    /// <summary>
    /// The last character in the range, inclusive.
    /// </summary>
    public TChar KeyTo { get; } = keyTo;

    /// <summary>
    /// The index of the target DFA state, starting from 0.
    /// </summary>
    /// <remarks>
    /// A negative value indicates that following this edge should stop the tokenizer.
    /// </remarks>
    public int Target { get; } = target;

    private string DebuggerDisplay()
    {
        string target = Target < 0 ? "<fail>" : Target.ToString();
        if (EqualityComparer<TChar>.Default.Equals(KeyFrom, KeyTo))
        {
            return $"{Format(KeyFrom)} -> {target}";
        }
        return $"[{Format(KeyFrom)},{Format(KeyTo)}] -> {target}";

    }

    internal static string Format(TChar c)
    {
        if (c is char c2)
        {
            return c2 switch
            {
                '\0' => "'\\0'",
                '\a' => "'\\a'",
                '\b' => "'\\b'",
                '\f' => "'\\f'",
                '\n' => "'\\n'",
                '\r' => "'\\r'",
                '\t' => "'\\t'",
                '\v' => "'\\v'",
                '\'' => "'\\''",
                '"' => "'\"'",
                '\\' => "'\\\\'",
                _ => c2 < 32 || c2 > 126 ? $"'\\u{(int)c2:X4}'" : $"'{c2}'"
            };
        }

        if (c is byte b)
        {
            return $"0x{b:X2}";
        }

        return $"'{c}'";
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
