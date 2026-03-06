// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Farkle.Grammars;

/// <summary>
/// Points to a <see cref="GroupDefinition"/> of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// <para>This type is lightweight, storing just a number without a <see cref="Grammar"/> object and can be
/// of use when parsing. To get any information about the group you have to pass it to the
/// <see cref="Grammar.GetGroup"/> method.</para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
public readonly struct GroupHandle : IEquatable<GroupHandle>
{
    internal uint TableIndex { get; }
    internal GroupHandle(uint tableIndex) => TableIndex = tableIndex;

    /// <summary>
    /// Gets the group's index in the grammar.
    /// </summary>
    /// <remarks>
    /// The first group has a value of zero.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The group's
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
    /// Whether this <see cref="GroupHandle"/> has a valid value.
    /// </summary>
    /// <seealso cref="Value"/>
    public bool HasValue => TableIndex != 0;

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay() => HasValue ? Value.ToString() : "<null>";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GroupHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(GroupHandle other) => TableIndex == other.TableIndex;

    /// <inheritdoc/>
    public override int GetHashCode() => TableIndex.GetHashCode();

    /// <summary>
    /// Checks if two <see cref="GroupHandle"/>s are pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(GroupHandle left, GroupHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="GroupHandle"/>s are not pointing to the same row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(GroupHandle left, GroupHandle right) => !(left==right);

    /// <summary>
    /// Implicitly converts a <see cref="GroupHandle"/> to an <see cref="EntityHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="GroupHandle"/> to convert.</param>
    public static implicit operator EntityHandle(GroupHandle handle) =>
        new(handle.TableIndex, TableKind.Group);

    /// <summary>
    /// Casts an <see cref="EntityHandle"/> to a <see cref="GroupHandle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="EntityHandle"/> to cast.</param>
    /// <exception cref="InvalidCastException"><paramref name="handle"/>'s <see cref="EntityHandle.IsGroup"/>
    /// property is <see langword="false"/>.</exception>
    public static explicit operator GroupHandle(EntityHandle handle)
    {
        if (!handle.HasValue)
        {
            return default;
        }
        handle.TypeCheck(TableKind.Group);
        return new(handle.TableIndex);
    }
}
