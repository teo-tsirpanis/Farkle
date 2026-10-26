// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Common;

/// <summary>
/// A key-value pair type that only considers the key for equality.
/// </summary>
/// <remarks>
/// This type is useful in incremental generator pipelines, when <see cref="Value"/> is hard or not possible to compare,
/// but entirely dependent on <see cref="Key"/>.
/// </remarks>
public readonly struct KeyAndValue<TKey, TValue>(TKey key, TValue value) : IEquatable<KeyAndValue<TKey, TValue>>
{
    public TKey Key { get; } = key;

    public TValue Value { get; } = value;

    public bool Equals(KeyAndValue<TKey, TValue> other) =>
        EqualityComparer<TKey>.Default.Equals(Key, other.Key);

    public override bool Equals(object? obj) =>
        obj is KeyAndValue<TKey, TValue> other && Equals(other);

    public override int GetHashCode() => Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key);
}

public static class KeyAndValue
{
    public static KeyAndValue<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value) => new(key, value);
}
