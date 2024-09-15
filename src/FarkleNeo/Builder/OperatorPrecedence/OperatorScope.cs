// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Farkle.Builder.OperatorPrecedence;

/// <summary>
/// Represents a collection of <see cref="AssociativityGroup"/>s that are
/// ordered by precedence and can be used to resolve conflicts when building
/// the parser state machine.
/// </summary>
#if false
// Temporarily delaying collection expression support, see below.
/// <remarks>
/// An operator scope can be created with C# 12's collection expressions.
/// </remarks>
[CollectionBuilder(typeof(OperatorScope), nameof(Create))]
#endif
public sealed class OperatorScope
#if false
    : IEnumerable<AssociativityGroup>
#endif
{
    /// <summary>
    /// Whether the operator scope can be used to resolve reduce-reduce conflicts.
    /// </summary>
    /// <remarks>
    /// This capability is not enabled by default.
    /// </remarks>
    /// <seealso cref="OperatorScope(bool, ImmutableArray{AssociativityGroup})"/>
    public bool CanResolveReduceReduceConflicts { get; }

    internal ImmutableArray<AssociativityGroup> AssociativityGroups { get; }

    /// <summary>
    /// Creates an <see cref="OperatorScope"/>.
    /// </summary>
    /// <param name="canResolveReduceReduceConflicts">The value of <see cref="CanResolveReduceReduceConflicts"/>.</param>
    /// <param name="associativityGroups">The <see cref="AssociativityGroup"/>s that will comprise the scope,
    /// in ascending order of precedence.</param>
    public OperatorScope(bool canResolveReduceReduceConflicts, params ImmutableArray<AssociativityGroup> associativityGroups)
    {
        if (associativityGroups.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(associativityGroups));
        }
        for (int i = 0; i < associativityGroups.Length; i++)
        {
            ArgumentNullExceptionCompat.ThrowIfNull(associativityGroups[i]);
        }
        CanResolveReduceReduceConflicts = canResolveReduceReduceConflicts;
        AssociativityGroups = associativityGroups;
    }

    /// <inheritdoc cref="OperatorScope(bool, System.Collections.Immutable.ImmutableArray{AssociativityGroup})"/>
    public OperatorScope(bool canResolveReduceReduceConflicts, params AssociativityGroup[] associativityGroups)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(associativityGroups);
        for (int i = 0; i < associativityGroups.Length; i++)
        {
            ArgumentNullExceptionCompat.ThrowIfNull(associativityGroups[i]);
        }
        CanResolveReduceReduceConflicts = canResolveReduceReduceConflicts;
        AssociativityGroups = associativityGroups.ToImmutableArray();
    }

    /// <inheritdoc cref="OperatorScope(bool, System.Collections.Immutable.ImmutableArray{AssociativityGroup})"/>
    public OperatorScope(params AssociativityGroup[] associativityGroups) : this(false, associativityGroups) { }

    /// <inheritdoc cref="OperatorScope(bool, System.Collections.Immutable.ImmutableArray{AssociativityGroup})"/>
    public OperatorScope(params ImmutableArray<AssociativityGroup> associativityGroups) : this(false, associativityGroups) { }

#if false
    // These APIs might not be necessary with https://github.com/dotnet/csharplang/pull/7895,
    // let's wait a bit before adding collection expressions support.

    /// <summary>
    /// Factory method to enable creating operator scopes using collection expressions.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static OperatorScope Create(ReadOnlySpan<AssociativityGroup> associativityGroups) =>
        new(false, associativityGroups.ToImmutableArray());

    /// <summary>
    /// Gets an enumerator for the scope's associativity groups.
    /// </summary>
    public ImmutableArray<AssociativityGroup>.Enumerator GetEnumerator() => AssociativityGroups.GetEnumerator();

    IEnumerator<AssociativityGroup> IEnumerable<AssociativityGroup>.GetEnumerator() =>
        ((IEnumerable<AssociativityGroup>)AssociativityGroups).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AssociativityGroups).GetEnumerator();
#endif
}
