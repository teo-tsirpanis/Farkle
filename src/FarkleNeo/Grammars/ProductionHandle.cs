// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a <see cref="Production"/> of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// <para>This type is lightweight, storing just a number without a <see cref="Grammar"/> object and can be
/// of use when parsing. To get any information about the production you have to pass it to the
/// <see cref="Grammar.GetProduction"/> method.</para>
/// </remarks>
public readonly struct ProductionHandle : IEquatable<ProductionHandle>
{
    internal uint TableIndex { get; }
    internal ProductionHandle(uint tableIndex) => TableIndex = tableIndex;

    /// <summary>
    /// Gets the production's index in the grammar.
    /// </summary>
    /// <remarks>
    /// The first production has a value of zero.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The production's
    /// <see cref="HasValue"/> property is false.</exception>
    /// <seealso cref="HasValue"/>
    public int Value
    {
        get
        {
            if (TableIndex == 0)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }
            return (int)TableIndex - 1;
        }
    }

    /// <summary>
    /// Whether this <see cref="ProductionHandle"/> has a valid value.
    /// </summary>
    /// <seealso cref="Value"/>
    public bool HasValue => TableIndex != 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProductionHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(ProductionHandle other) => TableIndex == other.TableIndex;

    /// <inheritdoc/>
    public override int GetHashCode() => TableIndex.GetHashCode();

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
        new(handle.TableIndex, TableKind.Production);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsProduction"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator ProductionHandle(EntityHandle handle)
    {
        if (!handle.HasValue)
        {
            return default;
        }
        handle.TypeCheck(TableKind.Production);
        return new(handle.TableIndex);
    }
}
