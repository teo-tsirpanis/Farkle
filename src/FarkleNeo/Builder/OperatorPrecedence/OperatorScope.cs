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
/// <remarks>
/// An operator scope can be created with C# 12's collection expressions.
/// </remarks>
[CollectionBuilder(typeof(OperatorScope), nameof(Create))]
public sealed class OperatorScope : IEnumerable<AssociativityGroup>
{
    /// <summary>
    /// Whether the operator scope can be used to resolve reduce-reduce conflicts.
    /// </summary>
    /// <remarks>
    /// This capability is not enabled by default.
    /// </remarks>
    /// <seealso cref="OperatorScope(bool, ReadOnlySpan{AssociativityGroup})"/>
    public bool CanResolveReduceReduceConflicts { get; }

    internal ImmutableArray<AssociativityGroup> AssociativityGroups { get; }

    /// <summary>
    /// Creates an <see cref="OperatorScope"/>.
    /// </summary>
    /// <param name="canResolveReduceReduceConflicts">The value of <see cref="CanResolveReduceReduceConflicts"/>.</param>
    /// <param name="associativityGroups">The <see cref="AssociativityGroup"/>s that will comprise the scope,
    /// in ascending order of precedence.</param>
    public OperatorScope(bool canResolveReduceReduceConflicts, params ReadOnlySpan<AssociativityGroup> associativityGroups)
    {
        for (int i = 0; i < associativityGroups.Length; i++)
        {
            ArgumentNullExceptionCompat.ThrowIfNull(associativityGroups[i]);
        }
        CanResolveReduceReduceConflicts = canResolveReduceReduceConflicts;
        AssociativityGroups = associativityGroups.ToImmutableArray();
    }

    /// <inheritdoc cref="OperatorScope(bool, ReadOnlySpan{AssociativityGroup})"/>
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

    /// <inheritdoc cref="OperatorScope(bool, ReadOnlySpan{AssociativityGroup})"/>
    public OperatorScope(params AssociativityGroup[] associativityGroups) : this(false, associativityGroups) { }

    /// <inheritdoc cref="OperatorScope(bool, ReadOnlySpan{AssociativityGroup})"/>
    public OperatorScope(params ReadOnlySpan<AssociativityGroup> associativityGroups) : this(false, associativityGroups) { }

    /// <summary>
    /// Factory method to enable creating operator scopes using collection expressions.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static OperatorScope Create(ReadOnlySpan<AssociativityGroup> associativityGroups) => new(associativityGroups);

    // An optimized GetEnumerator() that returns an immutable array enumerator will not
    // be provided at the moment due to the lack of use cases. It can be added in the future
    // if needed.

    IEnumerator<AssociativityGroup> IEnumerable<AssociativityGroup>.GetEnumerator() =>
        ((IEnumerable<AssociativityGroup>)AssociativityGroups).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AssociativityGroups).GetEnumerator();
}
