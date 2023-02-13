// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a blob of bytes in a <see cref="Grammar"/>.
/// </summary>
public readonly struct BlobHandle : IEquatable<BlobHandle>
{
    internal uint Value { get; }

    internal BlobHandle(uint value) => Value = value;

    /// <summary>
    /// Whether this <see cref="BlobHandle"/> points to the empty blob.
    /// </summary>
    public bool IsEmpty => Value == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BlobHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(BlobHandle other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="BlobHandle"/>s are pointing to the same blob.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(BlobHandle left, BlobHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="BlobHandle"/>s are not pointing to the same blob.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(BlobHandle left, BlobHandle right) => !(left==right);
}
