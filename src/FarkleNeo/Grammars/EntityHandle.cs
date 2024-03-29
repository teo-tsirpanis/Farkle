// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Points to a table row of a <see cref="Grammar"/>.
/// </summary>
public readonly struct EntityHandle : IEquatable<EntityHandle>
{
    internal const int ValueSize = 24;
    internal const uint ValueMask = (1 << ValueSize) - 1;

    internal readonly uint _valueAndKind;

    internal uint TableIndex => _valueAndKind & ValueMask;

    /// <summary>
    /// The <see cref="TableKind"/> of this handle.
    /// </summary>
    internal TableKind Kind => (TableKind)(_valueAndKind >> ValueSize);

    internal EntityHandle(uint value, TableKind kind)
    {
        Debug.Assert(value <= ValueMask);
        _valueAndKind = value == 0 ? 0 : (value & ValueMask) | ((uint)kind << ValueSize);
        Debug.Assert(!HasValue || IsTokenSymbol || IsNonterminal || IsProduction, "Cannot export this type of handle.");
    }

    internal void TypeCheck(TableKind expectedKind)
    {
        if (Kind != expectedKind)
        {
            ThrowHelpers.ThrowEntityHandleMismatch(expectedKind, Kind);
        }
    }

    /// <summary>
    /// Whether this <see cref="EntityHandle"/> has a valid value.
    /// </summary>
    public bool HasValue => TableIndex != 0;

    /// <summary>
    /// Whether this <see cref="EntityHandle"/> can be cast to a <see cref="TokenSymbolHandle"/>.
    /// </summary>
    public bool IsTokenSymbol => Kind == TableKind.TokenSymbol;

    /// <summary>
    /// Whether this <see cref="EntityHandle"/> can be cast to a <see cref="NonterminalHandle"/>.
    /// </summary>
    public bool IsNonterminal => Kind == TableKind.Nonterminal;

    /// <summary>
    /// Whether this <see cref="EntityHandle"/> can be cast to a <see cref="ProductionHandle"/>.
    /// </summary>
    public bool IsProduction => Kind == TableKind.Production;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EntityHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(EntityHandle other) => _valueAndKind == other._valueAndKind;

    /// <inheritdoc/>
    public override int GetHashCode() => _valueAndKind.GetHashCode();

    /// <summary>
    /// Returns a string describing the the <see cref="EntityHandle"/>.
    /// </summary>
    public override string ToString() => HasValue ? $"{Kind} {TableIndex + 1}" : "<null>";

    /// <summary>
    /// Checks if two <see cref="EntityHandle"/>s are pointing to the same table row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="EntityHandle"/>s are pointing to the same table row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(EntityHandle left, EntityHandle right) => !(left==right);
}
