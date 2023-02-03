// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a row of the <c>Production</c> table of a <see cref="Grammar"/>.
/// </summary>
public readonly struct ProductionHandle : IEquatable<ProductionHandle>
{
    internal uint Value { get; }
    internal ProductionHandle(uint value) => Value = value;

    /// <summary>
    /// Whether this <see cref="ProductionHandle"/> has a valid value.
    /// </summary>
    public bool IsNil => Value == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProductionHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(ProductionHandle other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="ProductionHandle"/>s are pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(ProductionHandle left, ProductionHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="ProductionHandle"/>s are not pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(ProductionHandle left, ProductionHandle right) => !(left==right);

    /// <summary>
    /// Implicitly converts a <see cref="ProductionHandle"/> to an <see cref="EntityHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="ProductionHandle"/> to convert.</param>
    public static implicit operator EntityHandle(ProductionHandle handle) =>
        new(handle.Value, TableKind.Production);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsProduction"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator ProductionHandle(EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return default;
        }
        handle.TypeCheck(TableKind.Production);
        return new(handle.Value);
    }
}
