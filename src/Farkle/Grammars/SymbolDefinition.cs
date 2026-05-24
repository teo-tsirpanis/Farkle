// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a symbol in the right-hand side of a <see cref="ProductionDefinition"/>.
/// </summary>
/// <remarks>
/// This type is a union of <see cref="TokenSymbolDefinition"/> and <see cref="NonterminalDefinition"/>.
/// </remarks>
/// <seealso cref="ProductionDefinition.Members"/>
// TODO-CSHARP15: Use union pattern matching.
[Union]
public readonly struct SymbolDefinition : IEquatable<SymbolDefinition>
#if NET11_0_OR_GREATER
    , IUnion
#endif
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="SymbolDefinition"/>'s <see cref="SymbolHandle"/>.
    /// </summary>
    public SymbolHandle Handle { get; }

    internal SymbolDefinition(Grammar grammar, SymbolHandle handle)
    {
        _grammar = grammar;
        Handle = handle;
    }

    /// <summary>
    /// Creates a new <see cref="SymbolDefinition"/> from a <see cref="TokenSymbolDefinition"/>.
    /// </summary>
    public SymbolDefinition(TokenSymbolDefinition tokenSymbol)
    {
        _grammar = tokenSymbol.Grammar;
        Handle = new(tokenSymbol.Handle);
    }

    /// <summary>
    /// Creates a new <see cref="SymbolDefinition"/> from a <see cref="NonterminalDefinition"/>.
    /// </summary>
    public SymbolDefinition(NonterminalDefinition nonterminal)
    {
        _grammar = nonterminal.Grammar;
        Handle = new(nonterminal.Handle);
    }

    /// <summary>
    /// Returns whether this <see cref="SymbolDefinition"/> value contains a <see cref="TokenSymbolDefinition"/>,
    /// and returns it if so.
    /// </summary>
    /// <param name="tokenSymbol">Will contain the <see cref="TokenSymbolDefinition"/> if this value contains one.</param>
    public bool TryGetValue(out TokenSymbolDefinition tokenSymbol)
    {
        if (Handle.TryGetValue(out TokenSymbolHandle tokenSymbolHandle))
        {
            tokenSymbol = new(_grammar, tokenSymbolHandle);
            return true;
        }
        tokenSymbol = default;
        return false;
    }

    /// <summary>
    /// Returns whether this <see cref="SymbolDefinition"/> value contains a <see cref="NonterminalDefinition"/>,
    /// and returns it if so.
    /// </summary>
    /// <param name="nonterminal">Will contain the <see cref="NonterminalDefinition"/> if this value contains one.</param>
    public bool TryGetValue(out NonterminalDefinition nonterminal)
    {
        if (Handle.TryGetValue(out NonterminalHandle nonterminalHandle))
        {
            nonterminal = new(_grammar, nonterminalHandle);
            return true;
        }
        nonterminal = default;
        return false;
    }

    /// <summary>
    /// The value contained in this <see cref="SymbolDefinition"/>.
    /// </summary>
    /// <value>
    /// An object of type <see cref="TokenSymbolDefinition"/> or <see cref="NonterminalDefinition"/>,
    /// depending on the kind of the contained symbol.
    /// </value>
    public object Value
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }

            if (Handle.TryGetValue(out TokenSymbolHandle tokenSymbolHandle))
            {
                return new TokenSymbolDefinition(_grammar, tokenSymbolHandle);
            }
            bool isNonterminal = Handle.TryGetValue(out NonterminalHandle nonterminalHandle);
            Debug.Assert(isNonterminal);
            return new NonterminalDefinition(_grammar, nonterminalHandle);
        }
    }

    /// <summary>
    /// The <see cref="SymbolDefinition"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            if (!Handle.HasValue)
            {
                ThrowHelpers.ThrowHandleHasNoValue();
            }

            return this switch
            {
                TokenSymbolDefinition tokenSymbol => tokenSymbol.Name,
                NonterminalDefinition nonterminal => nonterminal.Name,
            };
        }
    }

    /// <inheritdoc/>
    public bool Equals(SymbolDefinition other) => _grammar == other._grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SymbolDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="SymbolDefinition"/>.
    /// </summary>
    public override string ToString()
    {
        switch (Handle)
        {
            case TokenSymbolHandle tokenSymbolHandle:
                return new TokenSymbolDefinition(_grammar, tokenSymbolHandle).ToString();
            case NonterminalHandle nonterminalHandle:
                return new NonterminalDefinition(_grammar, nonterminalHandle).ToString();
            case null:
                return "";
        }
    }

    /// <summary>
    /// Compares two <see cref="SymbolDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first symbol.</param>
    /// <param name="right">The second symbol.</param>
    public static bool operator ==(SymbolDefinition left, SymbolDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="SymbolDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first symbol.</param>
    /// <param name="right">The second symbol.</param>
    public static bool operator !=(SymbolDefinition left, SymbolDefinition right) => !left.Equals(right);
}
