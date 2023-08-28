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

    internal (uint Offset, uint NextOffset) GetNestingBounds(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables)
    {
        uint firstNesting = grammarTables.GetGroupFirstNesting(grammarFile, Index);
        uint firstNestingOfNext = Index < (uint)grammarTables.GroupRowCount ? grammarTables.GetGroupFirstNesting(grammarFile, Index + 1) : (uint)grammarTables.GroupNestingRowCount + 1;
        Debug.Assert(firstNesting <= firstNestingOfNext);
        return (firstNesting, firstNestingOfNext);
    }

    internal bool CanGroupNest(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables, uint groupIndex)
    {
        (uint offset, uint nextOffset) = GetNestingBounds(grammarFile, in grammarTables);
        for (uint i = offset; i < nextOffset; i++)
        {
            uint nesting = grammarTables.GetGroupNestingGroup(grammarFile, i);
            if (nesting == groupIndex)
            {
                return true;
            }
        }
        return false;
    }

    internal bool CanGroupNest(uint groupIndex) => CanGroupNest(_grammar.GrammarFile, in _grammar.GrammarTables, groupIndex);

    /// <summary>
    /// A collection of the <see cref="Group"/>s that can be nested inside this <see cref="Group"/>.
    /// </summary>
    public GroupNestingCollection Nesting
    {
        get
        {
            AssertHasValue();
            (uint offset, uint nextOffset) = GetNestingBounds(_grammar.GrammarFile, in _grammar.GrammarTables);
            return new(_grammar, offset, (int)(nextOffset - offset));
        }
    }

    /// <summary>
    /// Returns a string describing the the <see cref="Group"/>.
    /// </summary>
    public override string ToString() => _grammar.GetString(Name);
}
