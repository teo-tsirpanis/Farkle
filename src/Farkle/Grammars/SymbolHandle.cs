// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars;

/// <summary>
/// Contains either a <see cref="TokenSymbolHandle"/> or a <see cref="NonterminalHandle"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
[Union]
public readonly struct SymbolHandle : IEquatable<SymbolHandle>
#if NET11_0_OR_GREATER
    , IUnion
#endif
{
    private const int KindSize = 1;
    private const int ValueSize = 24;
    private const uint ValueMask = (1 << (ValueSize + KindSize)) - 1;

    private readonly uint _codedIndex;

    internal uint TableIndex => _codedIndex >> KindSize;

    private bool IsTokenSymbol => (_codedIndex & 1) == 0;

    /// <summary>
    /// The <see cref="TableKind"/> of this handle.
    /// </summary>
    private TableKind Kind => IsTokenSymbol ? TableKind.TokenSymbol : TableKind.Nonterminal;

    internal SymbolHandle(uint codedIndex)
    {
        Debug.Assert(codedIndex <= ValueMask);
        _codedIndex = codedIndex;
    }

    /// <summary>
    /// Creates a new <see cref="SymbolHandle"/> from a <see cref="TokenSymbolHandle"/>.
    /// </summary>
    public SymbolHandle(TokenSymbolHandle tokenSymbolHandle)
    {
        _codedIndex = (tokenSymbolHandle.TableIndex << KindSize) | 0;
    }

    /// <summary>
    /// Creates a new <see cref="SymbolHandle"/> from a <see cref="NonterminalHandle"/>.
    /// </summary>
    public SymbolHandle(NonterminalHandle nonterminalHandle)
    {
        // Normalize null handles to have an index of 0, regardless of the kind they were created from.
        _codedIndex = nonterminalHandle.TableIndex;
        if (_codedIndex != 0)
        {
            _codedIndex = (_codedIndex << KindSize) | 1;
        }
    }

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay() => HasValue ? $"{Kind} {TableIndex + 1}" : "<null>";

    internal uint GetCodedIndex() => _codedIndex;

    /// <summary>
    /// Whether this <see cref="SymbolHandle"/> has a valid value.
    /// </summary>
    public bool HasValue => _codedIndex != 0;

    /// <summary>
    /// The value contained in this <see cref="SymbolHandle"/>.
    /// </summary>
    /// <value>An object of type <see cref="TokenSymbolHandle"/> or <see cref="NonterminalHandle"/>, depending on the
    /// kind of the contained handle. If <see cref="HasValue"/> is <see langword="false"/>, this property returns
    /// <see langword="null"/>.</value>
    public object? Value
    {
        get
        {
            if (!HasValue)
            {
                return null;
            }
            return IsTokenSymbol ? new TokenSymbolHandle(TableIndex) : new NonterminalHandle(TableIndex);
        }
    }

    /// <summary>
    /// Returns whether this <see cref="SymbolHandle"/> value contains a <see cref="TokenSymbolHandle"/>,
    /// and returns it if so.
    /// </summary>
    /// <param name="tokenSymbolHandle">Will contain the <see cref="TokenSymbolHandle"/> if this value contains one.</param>
    public bool TryGetValue(out TokenSymbolHandle tokenSymbolHandle)
    {
        if (HasValue && IsTokenSymbol)
        {
            tokenSymbolHandle = new(TableIndex);
            return true;
        }
        tokenSymbolHandle = default;
        return false;
    }

    /// <summary>
    /// Returns whether this <see cref="SymbolHandle"/> value contains a <see cref="NonterminalHandle"/>,
    /// and returns it if so.
    /// </summary>
    /// <param name="nonterminalHandle">Will contain the <see cref="NonterminalHandle"/> if this value contains one.</param>
    public bool TryGetValue(out NonterminalHandle nonterminalHandle)
    {
        if (HasValue && !IsTokenSymbol)
        {
            nonterminalHandle = new(TableIndex);
            return true;
        }
        nonterminalHandle = default;
        return false;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SymbolHandle handle && Equals(handle);

    /// <inheritdoc/>
    public bool Equals(SymbolHandle other) => _codedIndex == other._codedIndex;

    /// <inheritdoc/>
    public override int GetHashCode() => _codedIndex.GetHashCode();

    internal TokenSymbolHandle AsTokenSymbol()
    {
        if (!TryGetValue(out TokenSymbolHandle tokenSymbolHandle))
        {
            ThrowHelpers.ThrowInvalidCastException();
        }
        return tokenSymbolHandle;
    }

    internal NonterminalHandle AsNonterminal()
    {
        if (!TryGetValue(out NonterminalHandle nonterminalHandle))
        {
            ThrowHelpers.ThrowInvalidCastException();
        }
        return nonterminalHandle;
    }

    /// <summary>
    /// Checks if two <see cref="SymbolHandle"/>s are pointing to the same table row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator ==(SymbolHandle left, SymbolHandle right) => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="SymbolHandle"/>s are not pointing to the same table row.
    /// </summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <remarks>
    /// If <paramref name="left"/> and <paramref name="right"/> come
    /// from different <see cref="Grammar"/>s the result is undefined.
    /// </remarks>
    public static bool operator !=(SymbolHandle left, SymbolHandle right) => !(left==right);
}
