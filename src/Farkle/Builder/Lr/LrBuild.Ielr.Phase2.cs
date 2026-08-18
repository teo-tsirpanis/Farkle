// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using BitCollections;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

partial struct LrBuild
{
    private InadequacyAnnotationList ComputeAnnotations(Lr0StateMachine stateMachine,
        ImmutableArray<ConflictDescription> conflicts, ImmutableArray<TerminalSet> gotoFollows,
        ImmutableArray<TerminalSet> alwaysFollows, ImmutableArray<BitArrayNeo> predecessors,
        ImmutableArray<BitSet> gotoFollowKernelItems)
    {
        Log.Debug("Computing IELR annotations");

        var annotations = new HashSet<InadequacyAnnotation>();
        var annotationsToProcess = new Queue<InadequacyAnnotation>();
        var lookaheadSetCache = new ItemLookaheadSetCache(Syntax, stateMachine, gotoFollows, predecessors, CancellationToken);

        // Add annotations for each conflict (annotate_manifestations).
        for (int i = 0; i < conflicts.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            var conflict = conflicts[i];

            var builder = new InadequacyContributionMatrix.Builder(conflict.Contributions.Length);
            foreach (var contribution in conflict.Contributions)
            {
                if (contribution.IsShift(out _))
                {
                    builder.Add(null);
                }
                else if (contribution.IsReduce(out var production))
                {
                    var memberCount = Syntax.GetProductionMembers(production).Count;
                    if (memberCount == 0)
                    {
                        var head = Syntax.GetProductionHead(production.Index);
                        builder.Add(ComputeLeftHandContributions(conflict.StateIndex, head, conflict.Symbol, lookaheadSetCache));
                    }
                    else
                    {
                        int kernelItemIndex = stateMachine.States[conflict.StateIndex].KernelItems.IndexOf(new Lr0Item(production, memberCount));
                        Debug.Assert(kernelItemIndex >= 0);
                        builder.Add(BitSet.Singleton(kernelItemIndex));
                    }
                }
            }

            AddAnnotation(in this, new InadequacyAnnotation(conflict.StateIndex, i, builder.Build()));
        }

        // Propagate annotations to predecessor states (annotate_predecessor).
        while (annotationsToProcess.TryDequeue(out var annotation))
        {
            CancellationToken.ThrowIfCancellationRequested();

            var conflict = conflicts[annotation.ConflictIndex];

            foreach (var predecessor in predecessors[annotation.StateIndex])
            {
                var builder = new InadequacyContributionMatrix.Builder(annotation.ContributionMatrix.Count);
                foreach (BitSet? x in annotation.ContributionMatrix)
                {
                    if (x is not { } row)
                    {
                        builder.Add(null);
                        continue;
                    }
                    var newRow = BitSet.Empty;
                    foreach (var itemIndex in row)
                    {
                        var item = stateMachine.States[annotation.StateIndex].KernelItems[itemIndex];
                        Debug.Assert(item.DotPosition > 0);
                        switch (item.DotPosition)
                        {
                            case 1:
                                var head = Syntax.GetProductionHead(item.Production.Index);
                                var lhsContributions = ComputeLeftHandContributions(predecessor, head, conflict.Symbol, lookaheadSetCache);
                                if (lhsContributions is null)
                                {
                                    builder.Add(null);
                                    // TODO-CSHARP15: Use labeled continue.
                                    goto NextRow;
                                }
                                newRow = BitSet.Union(newRow, lhsContributions.Value);
                                break;
                            default:
                                var previousItem = new Lr0Item(item.Production, item.DotPosition - 1);
                                int previousItemIndex = stateMachine.States[predecessor].KernelItems.IndexOf(previousItem);
                                if (lookaheadSetCache.GetLookaheadSet(predecessor, previousItemIndex)[conflict.Symbol])
                                {
                                    newRow = newRow.Set(previousItemIndex, true);
                                }

                                break;
                        }
                    }
                    builder.Add(newRow);
                NextRow:;
                }

                AddAnnotation(in this, new InadequacyAnnotation(predecessor, annotation.ConflictIndex, builder.Build()));
            }
        }

        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Computed {annotations.Count} IELR annotations");
        }

        return new(stateMachine.States.Length, annotations);

        void AddAnnotation(in LrBuild @this, InadequacyAnnotation annotation)
        {
            if (@this.IsSplitStableDominantContribution(annotation, conflicts[annotation.ConflictIndex]))
            {
                return;
            }
            if (annotations.Add(annotation))
            {
                annotationsToProcess.Enqueue(annotation);
            }
        }

        BitSet? ComputeLeftHandContributions(int stateIndex, Symbol productionHead, Symbol conflictSymbol,
            in ItemLookaheadSetCache lookaheadSetCache)
        {
            int gotoIndex = stateMachine.States[stateIndex].Transitions[productionHead];
            if (alwaysFollows[gotoIndex][conflictSymbol])
            {
                return null;
            }
            BitSet result = BitSet.Empty;
            var kernelItems = stateMachine.States[stateIndex].KernelItems;
            for (int i = 0; i < kernelItems.Count; i++)
            {
                if (gotoFollowKernelItems[gotoIndex][i] && lookaheadSetCache.GetLookaheadSet(stateIndex, i)[conflictSymbol])
                {
                    result = result.Set(i, true);
                }
            }
            return result;
        }
    }

    private bool IsSplitStableDominantContribution(InadequacyAnnotation annotation, ConflictDescription conflict)
    {
        // An annotation specifies a split-stable dominant contribution if, after removing never contributions,
        // the set of contributions preferred by conflict resolution contains no potential contributions.
        // If conflict resolution is not specified by the user, we would always get CannotChoose, which reduces
        // this algorithm to the IELR paper's trivial definition of "the matrix contains only always or never
        // contributions".

        // We keep one contribution from the dominant set, and whether the dominant set contains a potential contribution.
        LrConflictContribution? dominantContribution = null;
        bool isPotentialContributionInDominantSet = false;
        var matrix = annotation.ContributionMatrix;
        for (int i = 0; i < matrix.Count; i++)
        {
            var classification = ClassifyContribution(matrix[i]);
            if (classification == InadequacyContributionClassification.Never)
            {
                continue;
            }
            bool isPotential = classification == InadequacyContributionClassification.Potential;
            var candidateContribution = conflict.Contributions[i];
            // This is the first non-never contribution we have seen; we include it in the dominant set.
            if (dominantContribution is null)
            {
                dominantContribution = candidateContribution;
                isPotentialContributionInDominantSet = isPotential;
                continue;
            }
            switch (ResolveConflict(conflict.Symbol, dominantContribution.Value, candidateContribution))
            {
                // The dominant contribution is preferred over the candidate contribution; we do nothing.
                case LrConflictResolverDecision.ChooseOption1:
                    break;
                // The candidate contribution is preferred over the dominant contribution; this becomes
                // the new dominant contribution.
                case LrConflictResolverDecision.ChooseOption2:
                    dominantContribution = candidateContribution;
                    isPotentialContributionInDominantSet = isPotential;
                    break;
                // The dominant contribution and the candidate contribution are equally preferred; we keep
                // the same dominant contribution, and update whether the dominant set contains a potential
                // contribution.
                case LrConflictResolverDecision.CannotChoose:
                // We do the same even if the conflict resolver prefers neither contribution, because per the
                // IELR paper's definition of split-stable dominant contribution, if one of the contributions
                // was potential, removing it would have given a different dominant set.
                case LrConflictResolverDecision.ChooseNeither:
                    isPotentialContributionInDominantSet |= isPotential;
                    break;
            }
        }
        return !isPotentialContributionInDominantSet;
    }

    private static InadequacyContributionClassification ClassifyContribution(BitSet? contribution) => contribution switch
    {
        null => InadequacyContributionClassification.Always,
        { IsEmpty: true } => InadequacyContributionClassification.Never,
        _ => InadequacyContributionClassification.Potential,
    };

    private readonly struct InadequacyAnnotationList : IReadOnlyCollection<InadequacyAnnotation>
    {
        private readonly ImmutableArray<InadequacyAnnotation> _annotations;

        private readonly int[] _firstAnnotationOfState;

        public InadequacyAnnotationList(int stateCount, IReadOnlyCollection<InadequacyAnnotation> annotations)
        {
            var annotationsBuilder = ImmutableArray.CreateBuilder<InadequacyAnnotation>(annotations.Count);
            annotationsBuilder.AddRange(annotations);
            annotationsBuilder.Sort(static (a, b) => a.StateIndex.CompareTo(b.StateIndex));
            _annotations = annotationsBuilder.MoveToImmutable();

            _firstAnnotationOfState = new int[stateCount];
            int currentStateIndex = 0;
            for (int i = 0; i < _annotations.Length; i++)
            {
                int stateIndex = _annotations[i].StateIndex;
                Debug.Assert(currentStateIndex <= stateIndex);
                while (currentStateIndex < stateIndex)
                {
                    _firstAnnotationOfState[currentStateIndex++] = i;
                }
            }
        }

        public int Count => _annotations.Length;

        public IEnumerator<InadequacyAnnotation> GetEnumerator() => ((IEnumerable<InadequacyAnnotation>)_annotations).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_annotations).GetEnumerator();

        public ReadOnlySpan<InadequacyAnnotation> GetAnnotations(int stateIndex)
        {
            int firstAnnotation = _firstAnnotationOfState[stateIndex];
            int firstAnnotationOfNext = stateIndex + 1 < _firstAnnotationOfState.Length ? _firstAnnotationOfState[stateIndex + 1] : _annotations.Length;
            return _annotations.AsSpan()[firstAnnotation..(firstAnnotationOfNext - firstAnnotation)];
        }
    }

    private sealed class InadequacyAnnotation(int stateIndex, int conflictIndex, InadequacyContributionMatrix contributionMatrix) : IEquatable<InadequacyAnnotation>
    {
        public int StateIndex { get; } = stateIndex;

        public int ConflictIndex { get; } = conflictIndex;

        public InadequacyContributionMatrix ContributionMatrix { get; } = contributionMatrix;

        public bool Equals(InadequacyAnnotation? other) =>
            other is not null
            && StateIndex == other.StateIndex
            && ConflictIndex == other.ConflictIndex
            && ContributionMatrix.Equals(other.ContributionMatrix);

        public override bool Equals(object? obj) => obj is InadequacyAnnotation other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(StateIndex, ConflictIndex, ContributionMatrix);
    }

    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    private readonly struct InadequacyContributionMatrix(ImmutableArray<BitSet> matrix, BitSet definedRows) : IEquatable<InadequacyContributionMatrix>, IReadOnlyCollection<BitSet?>
    {
        private readonly ImmutableArray<BitSet> _matrix = matrix;

        private readonly BitSet _definedRows = definedRows;

        public int Count => _matrix.Length;

        public BitSet? this[int index] => _definedRows[index] ? _matrix[index] : null;

        [ExcludeFromCodeCoverage]
        private string GetDebuggerDisplay()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                var row = this[i];
                if (row is not { } r)
                {
                    MaybeAddSeparator();
                    sb.Append($"γ[{i}] = undef");
                    continue;
                }
                foreach (var column in r)
                {
                    MaybeAddSeparator();
                    sb.Append($"γ[{i}][{column}] = true");
                }
            }
            return sb.ToString();

            void MaybeAddSeparator()
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
            }
        }

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<BitSet?> IEnumerable<BitSet?>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(InadequacyContributionMatrix other) => _definedRows.Equals(other._definedRows) && _matrix.SequenceEqual(other._matrix);

        public override bool Equals(object? obj) => obj is InadequacyContributionMatrix other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_definedRows);
            hash.Add(_matrix.Length);
            foreach (var row in _matrix)
            {
                hash.Add(row);
            }
            return hash.ToHashCode();
        }

        public struct Builder(int contributionsCount)
        {
            private readonly ImmutableArray<BitSet>.Builder _matrix = ImmutableArray.CreateBuilder<BitSet>(contributionsCount);

            private BitSet _definedRows = BitSet.Empty;

            public void Add(BitSet? row)
            {
                if (row is not null)
                {
                    _definedRows = _definedRows.Set(_matrix.Count, true);
                }
                _matrix.Add(row ?? BitSet.Empty);
            }

            public readonly InadequacyContributionMatrix Build() => new(_matrix.MoveToImmutable(), _definedRows);
        }

        public struct Enumerator(InadequacyContributionMatrix matrix) : IEnumerator<BitSet?>
        {
            private int _index = -1;

            public readonly BitSet? Current => matrix[_index];

            readonly object? IEnumerator.Current => Current;

            public readonly void Dispose() { }

            public bool MoveNext()
            {
                if (_index == matrix.Count)
                {
                    return false;
                }
                _index++;
                return true;
            }

            public void Reset() => _index = -1;
        }
    }

    private enum InadequacyContributionClassification
    {
        Always,
        Potential,
        Never,
    }

    /// <summary>
    /// Contains the logic to compute kernel item lookahead sets.
    /// </summary>
    /// <remarks>
    /// This type employs memoization to avoid repeated computations, so
    /// you should reuse the same instance for performance reasons.
    /// </remarks>
    private readonly struct ItemLookaheadSetCache(AugmentedSyntaxProvider syntax, Lr0StateMachine stateMachine,
        ImmutableArray<TerminalSet> gotoFollows, ImmutableArray<BitArrayNeo> predecessors, CancellationToken cancellationToken)
    {
        private readonly TerminalSet[]?[] _cache = new TerminalSet[stateMachine.States.Length][];

        private readonly Stack<(int StateIndex, int ItemIndex, TerminalSet ResultAccumulator)> _stack = [];

        private TerminalSet NewResultArray() => new(syntax);

        /// <summary>
        /// Computes the lookahead set of specified kernel item in the specified state.
        /// </summary>
        public TerminalSet GetLookaheadSet(int stateIndex, int itemIndex)
        {
            if (_cache[stateIndex]?[itemIndex] is { IsDefault: false } result)
            {
                return result;
            }

            Debug.Assert(_stack.Count == 0);

            _stack.Push((stateIndex, itemIndex, NewResultArray()));

            while (_stack.TryPeek(out var top))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Set is already computed; nothing to do here.
                if (!(_cache[top.StateIndex]?[top.ItemIndex] ?? default).IsDefault)
                {
                    _stack.Pop();
                    continue;
                }

                var item = stateMachine.States[top.StateIndex].KernelItems[top.ItemIndex];
                bool hasResult = true;
                switch (item.DotPosition)
                {
                    // The only kernel item with a dot at the beginning is <S'> → • <S> and has an empty lookahead set.
                    // We won't normally encounter this case, but we handle it for completeness.
                    case 0:
                        break;
                    // The item's dot is one position after the beginning. We accumulate the follow sets of
                    // the GOTOs in the predecessor states that transition to the item's production head.
                    case 1:
                        var productionHead = syntax.GetProductionHead(item.Production.Index);
                        foreach (var predecessor in predecessors[top.StateIndex])
                        {
                            int gotoIdx = stateMachine.States[predecessor].Transitions[productionHead];
                            top.ResultAccumulator.Or(gotoFollows[gotoIdx]);
                        }
                        break;
                    // The item's dot is later in the production.
                    // We accumulate the lookahead sets of the kernel items in the predecessor states with the same
                    // production and the dot one position earlier.
                    case > 1:
                        var previousItem = new Lr0Item(item.Production, item.DotPosition - 1);
                        foreach (var predecessor in predecessors[top.StateIndex])
                        {
                            var itemIdx = stateMachine.States[predecessor].KernelItems.IndexOf(previousItem);
                            Debug.Assert(itemIdx >= 0);
                            if (_cache[predecessor]?[itemIdx] is not { IsDefault: false } lookaheadSet)
                            {
                                // The predecessor is not yet computed. Push it to the stack, without popping the
                                // current item, so that we resume here after the predecessors are computed.
                                _stack.Push((predecessor, itemIdx, NewResultArray()));
                                hasResult = false;
                                continue;
                            }
                            // There will be some duplicate ORs if we are resuming a previous computation, but that's
                            // fine; the result will still be correct, and the performance impact does not seem terrible.
                            top.ResultAccumulator.Or(lookaheadSet);
                        }
                        break;
                }
                if (hasResult)
                {
                    TerminalSet[] stateCache = _cache[top.StateIndex] ??= new TerminalSet[stateMachine.States[top.StateIndex].KernelItems.Count];
                    stateCache[top.ItemIndex] = top.ResultAccumulator;
                    _stack.Pop();
                }
            }

            result = _cache[stateIndex]?[itemIndex] ?? default;
            Debug.Assert(!result.IsDefault);
            return result;
        }
    }
}
