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

    private readonly uint _tableIndex;

    internal Group(Grammar grammar, uint tableIndex)
    {
        _grammar = grammar;
        _tableIndex = tableIndex;
    }

    [StackTraceHidden]
    private void AssertHasValue()
    {
        Debug.Assert(_grammar is not null);
        if (_tableIndex == 0)
        {
            ThrowHelpers.ThrowHandleHasNoValue();
        }
    }

    /// <summary>
    /// A <see cref="StringHandle"/> to the <see cref="Group"/>'s name.
    /// </summary>
    public StringHandle NameHandle
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupName(_grammar.GrammarFile, _tableIndex);
        }
    }

    /// <summary>
    /// The <see cref="Group"/>'s name.
    /// </summary>
    public string Name
    {
        get
        {
            AssertHasValue();
            ReadOnlySpan<byte> grammarFile = _grammar.GrammarFile;
            StringHandle handle = _grammar.GrammarTables.GetGroupName(_grammar.GrammarFile, _tableIndex);
            return _grammar.StringHeap.GetString(grammarFile, handle);
        }
    }

    /// <summary>
    /// A <see cref="TokenSymbolHandle"/> pointing to the token symbol that represents this <see cref="Group"/>'s content.
    /// </summary>
    public TokenSymbolHandle Container
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupContainer(_grammar.GrammarFile, _tableIndex);
        }
    }

    /// <summary>
    /// The <see cref="Group"/>'s <see cref="GroupAttributes"/>.
    /// </summary>
    public GroupAttributes Flags
    {
        get
        {
            AssertHasValue();
            return _grammar.GrammarTables.GetGroupFlags(_grammar.GrammarFile, _tableIndex);
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
            return _grammar.GrammarTables.GetGroupStart(_grammar.GrammarFile, _tableIndex);
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
            return _grammar.GrammarTables.GetGroupEnd(_grammar.GrammarFile, _tableIndex);
        }
    }
}
