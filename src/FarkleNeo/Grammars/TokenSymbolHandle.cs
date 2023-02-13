// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Points to a row of the <c>TokenSymbol</c> table of a <see cref="Grammar"/>.
/// </summary>
public readonly struct TokenSymbolHandle : IEquatable<TokenSymbolHandle>
{
    internal uint TableIndex { get; }
    internal TokenSymbolHandle(uint tableIndex) => TableIndex = tableIndex;

    /// <summary>
    /// Gets the token symbol's index in the grammar.
    /// </summary>
    /// <remarks>
    /// The first token symbol has a value of zero.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The token symbol's
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
    /// Whether this <see cref="TokenSymbolHandle"/> has a valid value.
    /// </summary>
    /// <seealso cref="Value"/>
    public bool HasValue => TableIndex == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TokenSymbolHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(TokenSymbolHandle other) => TableIndex == other.TableIndex;

    /// <inheritdoc/>
    public override int GetHashCode() => TableIndex.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="TokenSymbolHandle"/>s are pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(TokenSymbolHandle left, TokenSymbolHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="TokenSymbolHandle"/>s are not pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(TokenSymbolHandle left, TokenSymbolHandle right) => !(left==right);

    /// <summary>
    /// Implicitly converts a <see cref="TokenSymbolHandle"/> to an <see cref="EntityHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="TokenSymbolHandle"/> to convert.</param>
    public static implicit operator EntityHandle(TokenSymbolHandle handle) =>
        new(handle.TableIndex, TableKind.TokenSymbol);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="TokenSymbolHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsTokenSymbol"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator TokenSymbolHandle(EntityHandle handle)
    {
        if (handle.HasValue)
        {
            return default;
        }
        handle.TypeCheck(TableKind.TokenSymbol);
        return new(handle.TableIndex);
    }
}
