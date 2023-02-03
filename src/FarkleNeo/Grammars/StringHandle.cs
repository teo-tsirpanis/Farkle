// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a string in a <see cref="Grammar"/>.
/// </summary>
public readonly struct StringHandle : IEquatable<StringHandle>
{
    internal uint Value { get; }

    internal StringHandle(uint value) => Value = value;

    /// <summary>
    /// Whether this <see cref="StringHandle"/> points to the empty string.
    /// </summary>
    public bool IsNil => Value == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StringHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(StringHandle other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="StringHandle"/>s are pointing to the same string.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(StringHandle left, StringHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="StringHandle"/>s are not pointing to the same string.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(StringHandle left, StringHandle right) => !(left==right);
}
