// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a row of the <c>Nonterminal</c> table of a <see cref="Grammar"/>.
/// </summary>
public readonly struct NonterminalHandle : IEquatable<NonterminalHandle>
{
    internal uint Value { get; }
    internal NonterminalHandle(uint value) => Value = value;

    /// <summary>
    /// Whether this <see cref="NonterminalHandle"/> has a valid value.
    /// </summary>
    public bool IsNil => Value == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NonterminalHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(NonterminalHandle other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="NonterminalHandle"/>s are pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(NonterminalHandle left, NonterminalHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="NonterminalHandle"/>s are not pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(NonterminalHandle left, NonterminalHandle right) => !(left==right);

    /// <summary>
    /// Implicitly converts a <see cref="NonterminalHandle"/> to an <see cref="EntityHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="NonterminalHandle"/> to convert.</param>
    public static implicit operator EntityHandle(NonterminalHandle handle) =>
        new(handle.Value, TableKind.Nonterminal);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="NonterminalHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsNonterminal"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator NonterminalHandle(EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return default;
        }
        handle.TypeCheck(TableKind.Nonterminal);
        return new(handle.Value);
    }
}
