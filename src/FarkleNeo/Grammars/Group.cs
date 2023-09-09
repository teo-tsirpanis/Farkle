// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Represents a group in a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// <para>Groups are lexical constructs that begin and end
/// with specific token symbols, contain arbitrary text
/// or nested groups and are contained in one token symbol.</para>
/// <para>A typical use of groups is in implementing comments.</para>
/// </remarks>
public readonly struct Group
{
    private readonly Grammar _grammar;

    internal uint Index { get; }

    internal Group(Grammar grammar, uint tableIndex)
    {
        _grammar = grammar;
        Index = tableIndex;
    }

    [StackTraceHidden]
    private void AssertHasValue()
    {
        Debug.Assert(_grammar is not null);
        if (Index == 0)
        {
            ThrowHelpers.ThrowHandleHasNoValue();
        }
    }

    /// <summary>
    /// A <see cref="StringHandle"/> pointing to the <see cref="Group"/>'s name.
    /// </summary>
    public StringHandle Name
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupName(_grammar.GrammarFile, Index);
        }
    }

    /// <summary>
    /// A <see cref="TokenSymbolHandle"/> pointing to the token symbol that represents the <see cref="Group"/>'s content.
    /// </summary>
    public TokenSymbolHandle Container
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupContainer(_grammar.GrammarFile, Index);
        }
    }

    /// <summary>
    /// The <see cref="Group"/>'s <see cref="GroupAttributes"/>.
    /// </summary>
    public GroupAttributes Attributes
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupFlags(_grammar.GrammarFile, Index);
        }
    }

    /// <summary>
    /// A <see cref="TokenSymbolHandle"/> pointing to the token symbol that starts this <see cref="Group"/>.
    /// </summary>
    public TokenSymbolHandle Start
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupStart(_grammar.GrammarFile, Index);
        }
    }

    /// <summary>
    /// A <see cref="TokenSymbolHandle"/> pointing to the token symbol that ends this <see cref="Group"/>.
    /// </summary>
    public TokenSymbolHandle End
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupEnd(_grammar.GrammarFile, Index);
        }
    }

    /// <summary>
    /// A collection of the <see cref="Group"/>s that can be nested inside this <see cref="Group"/>.
    /// </summary>
    public GroupNestingCollection Nesting
    {
        get
        {
            AssertHasValue();
            (uint offset, uint nextOffset) = _grammar.GrammarTables.GetGroupNestingBounds(_grammar.GrammarFile, Index);
            return new(_grammar, offset, (int)(nextOffset - offset));
        }
    }

    /// <summary>
    /// Returns a string describing the the <see cref="Group"/>.
    /// </summary>
    public override string ToString() => _grammar.GetString(Name);
}
