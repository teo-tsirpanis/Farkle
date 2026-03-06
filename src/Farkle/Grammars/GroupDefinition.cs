// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a group in a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// <para>Groups are lexical constructs that begin and end
/// with specific token symbols, contain arbitrary text
/// or nested groups and are contained in one token symbol.</para>
/// <para>A typical use of groups is in implementing comments.</para>
/// </remarks>
public readonly struct GroupDefinition : IEquatable<GroupDefinition>
{
    private readonly Grammar _grammar;

    /// <summary>
    /// The <see cref="GroupDefinition"/>'s <see cref="GroupHandle"/>.
    /// </summary>
    public GroupHandle Handle { get; }

    internal GroupDefinition(Grammar grammar, GroupHandle handle)
    {
        _grammar = grammar;
        Handle = handle;
    }

    [StackTraceHidden]
    private void AssertHasValue()
    {
        Debug.Assert(_grammar is not null);
        if (!Handle.HasValue)
        {
            ThrowHelpers.ThrowHandleHasNoValue();
        }
    }

    /// <summary>
    /// The <see cref="GroupDefinition"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            AssertHasValue();
            return _grammar.GetGroupName(Handle);
        }
    }

    /// <summary>
    /// The token symbol that represents the <see cref="GroupDefinition"/>'s content.
    /// </summary>
    public TokenSymbolDefinition Container
    {
        get
        {
            AssertHasValue();
            return new(_grammar, _grammar.GrammarTables.GetGroupContainer(_grammar.GrammarFile, Handle.TableIndex));
        }
    }

    /// <summary>
    /// The <see cref="GroupDefinition"/>'s <see cref="GroupAttributes"/>.
    /// </summary>
    public GroupAttributes Attributes
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupFlags(_grammar.GrammarFile, Handle.TableIndex);
        }
    }

    /// <summary>
    /// The token symbol that starts this <see cref="GroupDefinition"/>.
    /// </summary>
    public TokenSymbolDefinition Start
    {
        get
        {
            AssertHasValue();
            return new(_grammar, _grammar.GrammarTables.GetGroupStart(_grammar.GrammarFile, Handle.TableIndex));
        }
    }

    /// <summary>
    /// The token symbol that ends this <see cref="GroupDefinition"/>.
    /// </summary>
    public TokenSymbolDefinition End
    {
        get
        {
            AssertHasValue();
            return new(_grammar, _grammar.GrammarTables.GetGroupEnd(_grammar.GrammarFile, Handle.TableIndex));
        }
    }

    /// <summary>
    /// A collection of the <see cref="GroupDefinition"/>s that can be nested inside this <see cref="GroupDefinition"/>.
    /// </summary>
    public GroupNestingCollection Nesting
    {
        get
        {
            AssertHasValue();
            (uint offset, int count) = _grammar.GrammarTables.GetGroupNestingBounds(_grammar.GrammarFile, Handle.TableIndex);
            return new(_grammar, offset, count);
        }
    }

#pragma warning disable IDE0051 // Remove unused private members
    private int? DfaStartStateOnChar => _grammar.DfaOnChar?.GetStartStateForGroup(Handle);
#pragma warning restore IDE0051 // Remove unused private members

    /// <inheritdoc/>
    public bool Equals(GroupDefinition other) => _grammar == other._grammar && Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GroupDefinition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_grammar, Handle);

    /// <summary>
    /// Returns a string describing the <see cref="GroupDefinition"/>.
    /// </summary>
    public override string ToString() => _grammar is null ? "" : Name;

    /// <summary>
    /// Compares two <see cref="GroupDefinition"/>s for equality.
    /// </summary>
    /// <param name="left">The first group.</param>
    /// <param name="right">The second group.</param>
    public static bool operator ==(GroupDefinition left, GroupDefinition right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="GroupDefinition"/>s for inequality.
    /// </summary>
    /// <param name="left">The first group.</param>
    /// <param name="right">The second group.</param>
    public static bool operator !=(GroupDefinition left, GroupDefinition right) => !left.Equals(right);
}
