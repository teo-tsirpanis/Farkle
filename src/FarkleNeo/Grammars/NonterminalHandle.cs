// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a row of the <c>Nonterminal</c> table of a <see cref="Grammar"/>.
/// </summary>
public readonly struct NonterminalHandle : IEquatable<NonterminalHandle>
{
    internal uint TableIndex { get; }
    internal NonterminalHandle(uint tableIndex) => TableIndex = tableIndex;

    /// <summary>
    /// Gets the nonterminal's index in the grammar.
    /// </summary>
    /// <remarks>
    /// The first nonterminal has a value of zero.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The nonterminal's
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
    /// Whether this <see cref="NonterminalHandle"/> has a valid value.
    /// </summary>
    /// <seealso cref="Value"/>
    public bool HasValue => TableIndex == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NonterminalHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(NonterminalHandle other) => TableIndex == other.TableIndex;

    /// <inheritdoc/>
    public override int GetHashCode() => TableIndex.GetHashCode();

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
        new(handle.TableIndex, TableKind.Nonterminal);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="NonterminalHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsNonterminal"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator NonterminalHandle(EntityHandle handle)
    {
        if (handle.HasValue)
        {
            return default;
        }
        handle.TypeCheck(TableKind.Nonterminal);
        return new(handle.TableIndex);
    }
}
