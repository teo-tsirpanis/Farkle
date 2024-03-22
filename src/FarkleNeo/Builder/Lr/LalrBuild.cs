// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using BitCollections;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Contains the logic for building an LALR(1) state machine from a set of
/// syntax rules.
/// </summary>
internal readonly struct LalrBuild
{
    private readonly AugmentedSyntaxProvider Syntax;

    private readonly CancellationToken CancellationToken;

    private readonly BuilderLogger Log;

    private LalrBuild(IGrammarSyntaxProvider syntax, BuilderLogger log, CancellationToken cancellationToken)
    {
        Syntax = new(syntax);
        CancellationToken = cancellationToken;
        Log = log;
    }

    /// <summary>
    /// Computes for each production the index of the first member where this and all subsequent
    /// members are nullable.
    /// </summary>
    private ImmutableArray<int> ComputeProductionNullableStarts(BitArrayNeo nullableNonterminals)
    {
        Log.Debug("Computing production nullable starts");
        var starts = ImmutableArray.CreateBuilder<int>(Syntax.ProductionCount);
        foreach (Production p in Syntax.AllProductions)
        {
            ProductionMemberList members = Syntax.GetProductionMembers(p);
            int i = members.Count;
            while (i > 0)
            {
                Symbol s = members[i - 1];
                bool isNullable = s.IsTerminal ? !s.Equals(Syntax.EndSymbol) : nullableNonterminals[s.Index];
                if (!isNullable)
                {
                    break;
                }
                i--;
            }
            starts.Add(i);
        }
        Log.Debug("Computed production nullable starts");
        return starts.MoveToImmutable();
    }

    /// <summary>
    /// Computes which nonterminals can match the empty string.
    /// </summary>
    private BitArrayNeo ComputeNullableNonterminals()
    {
        Log.Debug("Computing nullable nonterminals");
        // A bit array to keep track of which nonterminals are either nullable,
        // or not yet determined to be nullable.
        var nullable = new BitArrayNeo(Syntax.NonterminalCount);
        // We use this variable to stop looping if no new nonteminals get marked
        // as nullable.
        bool changed;
        int iterations = 0;
        // TODO-PERF: Measure the algorithm's performance and consider if it would
        // be worth to track the definitely not nullable nonterminals in a separate
        // bit array to avoid needless checks, and if it would be further worth to
        // track the nullability of individual productions.
        do
        {
            // The following triple nested loop is actually running in linear time
            // over the total number of production members. No need to poll for
            // cancellation more often.
            CancellationToken.ThrowIfCancellationRequested();

            changed = false;
            iterations++;
            for (int i = 0; i < Syntax.NonterminalCount; i++)
            {
                if (nullable[i])
                {
                    continue;
                }
                foreach (Production p in Syntax.EnumerateNonterminalProductions(i))
                {
                    bool isProductionNullable = true;
                    foreach (Symbol s in Syntax.GetProductionMembers(p))
                    {
                        // The end symbol is always nullable.
                        if (s.IsTerminal && !s.Equals(Syntax.EndSymbol))
                        {
                            isProductionNullable = false;
                            break;
                        }

                        if (!nullable[s.Index])
                        {
                            isProductionNullable = false;
                            break;
                        }
                    }
                    if (isProductionNullable)
                    {
                        nullable[i] = true;
                        changed = true;
                        goto nextNonterminal;
                    }
                }
            nextNonterminal:;
            }
        } while (changed);
        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Computed nullable nonterminals after {iterations} iterations");
            if (Log.IsEnabled(DiagnosticSeverity.Verbose))
            {
                Log.Verbose($"Nullable nonterminals: {nullable}");
            }
        }
        return nullable;
    }

    /// <summary>
    /// Computes the LR(0) states.
    /// </summary>
    private ImmutableArray<Lr0State> ComputeStates()
    {
        Log.Debug("Computing states...");
        // These variables are global to the whole process.
        var states = ImmutableArray.CreateBuilder<Lr0State>();
        var kernelItemSetsToProcess = new Queue<KernelItemSet>();
#if DEBUG
        // Keeps track of the order each item gets added. In previous versions,
        // the state was being added while processing its predecessor's transitions,
        // and its own transitions were assigned later. Now we add only complete
        // states to the list, and during processing of transitions we enqueue
        // the kernel item sets to be later converted into a complete state.
        // This shadow queue exists to ensure the order of processing states is
        // the expected one.
        var indexShadowQueue = new Queue<int>();
#endif
        var kernelItemMap = new Dictionary<KernelItemSet, int>();
        // These variables are local to each step, but we declare them
        // outside the loop and reuse them for performance.
        var itemsToProcess = new Queue<Lr0Item>();
        var visitedItems = new HashSet<Lr0Item>();
        // This has to be a sorted dictionary to ensure that new states
        // are being created in a deterministic order.
        var grouppedTransitions = new SortedDictionary<Symbol, HashSet<Lr0Item>>();

        _ = GetOrQueueItemSet(new([.. Syntax.EnumerateNonterminalProductions(Syntax.StartSymbol.Index)]));

        while (kernelItemSetsToProcess.TryDequeue(out var kernelItems))
        {
#if DEBUG
            Debug.Assert(indexShadowQueue.Dequeue() == states.Count, "Incorrect state processing order.");
#endif
            foreach (var item in kernelItems)
            {
                itemsToProcess.Enqueue(item);
            }

            while (itemsToProcess.TryDequeue(out var item))
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (!visitedItems.Add(item))
                {
                    continue;
                }
                if (!TryAdvanceItem(item, out var symbol, out var nextItem))
                {
                    continue;
                }

                if (!symbol.IsTerminal)
                {
                    foreach (var prod in Syntax.EnumerateNonterminalProductions(symbol.Index))
                    {
                        itemsToProcess.Enqueue(prod);
                    }
                }

                if (!grouppedTransitions.TryGetValue(symbol, out var nextItems))
                {
                    nextItems = [];
                    grouppedTransitions.Add(symbol, nextItems);
                }
                nextItems.Add(nextItem);
            }

            var transitions = new Dictionary<Symbol, int>();
            foreach (var x in grouppedTransitions)
            {
                transitions.Add(x.Key, GetOrQueueItemSet(new(x.Value)));
            }
            states.Add(new Lr0State(kernelItems, transitions));

            Debug.Assert(itemsToProcess.Count == 0);
            itemsToProcess.Clear();
            grouppedTransitions.Clear();
        }

        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Created {states.Count} states.");
        }

        return states.DrainToImmutable();

        // Gets the index of a kernel item set, or queues it for processing if it
        // was first encountered. In the latter case, it returns a "phantom" index
        // that does not correspond to any materialized state yet.
        int GetOrQueueItemSet(KernelItemSet items)
        {
            if (kernelItemMap.TryGetValue(items, out int index))
            {
                return index;
            }
            index = kernelItemMap.Count;
#if DEBUG
            indexShadowQueue.Enqueue(index);
#endif
            kernelItemMap.Add(items, index);
            kernelItemSetsToProcess.Enqueue(items);
            return index;
        }
    }

    /// <summary>
    /// Tries to move the dot of an LR(0) item one position to the right.
    /// </summary>
    /// <param name="item">The LR(0) item.</param>
    /// <param name="symbol">The symbol at the dot of <paramref name="item"/>.</param>
    /// <param name="nextItem"><paramref name="item"/> with the dot being moved one
    /// position to the right.</param>
    /// <returns>Whether the dot of <paramref name="item"/> could be further advanced,
    /// i.e. wether it was not at the end of the production.</returns>
    private bool TryAdvanceItem(Lr0Item item, out Symbol symbol, out Lr0Item nextItem)
    {
        var productionMembers = Syntax.GetProductionMembers(item.Production);
        if (item.DotPosition >= productionMembers.Count)
        {
            symbol = default;
            nextItem = default;
            return false;
        }
        symbol = productionMembers[item.DotPosition];
        nextItem = new(item.Production, item.DotPosition + 1);
        return true;
    }

    /// <summary>
    /// Contains an LR(0) state machine of a grammar. This is the same as an LALR(1) state, but
    /// without lookahead sets.
    /// </summary>
    private readonly struct Lr0StateMachine(ImmutableArray<Lr0State> states, ImmutableArray<GotoInfo> gotos)
    {
        /// <summary>
        /// The states of the state machine.
        /// </summary>
        public ImmutableArray<Lr0State> States { get; } = states;

        /// <summary>
        /// The GOTO transitions of the state machine. The algorithm needs to store them
        /// separately to be able to compute properties on the transitions themselves.
        /// </summary>
        public ImmutableArray<GotoInfo> Gotos { get; } = gotos;
    }

    /// <summary>
    /// Contains detailed information about a GOTO transition.
    /// </summary>
    private readonly struct GotoInfo(int fromState, int toState, int nonterminal)
    {
        /// <summary>
        /// The state from which the GOTO originates.
        /// </summary>
        public int FromState { get; } = fromState;

        /// <summary>
        /// The state to which the GOTO leads.
        /// </summary>
        public int ToState { get; } = toState;

        /// <summary>
        /// The index of the nonterminal that triggers the GOTO.
        /// </summary>
        public int Symbol { get; } = nonterminal;
    }

    /// <summary>
    /// Represents an LR(0) state. This is the same as an LALR(1) state, but
    /// without the lookahead sets.
    /// </summary>
    private readonly struct Lr0State(KernelItemSet kernelItems, Dictionary<Symbol, int> transitions)
    {
        /// <summary>
        /// The set of kernel items that make up the state.
        /// </summary>
        public KernelItemSet KernelItems { get; } = kernelItems;

        /// <summary>
        /// The transitions from this state to other states. Do not modify.
        /// </summary>
        public Dictionary<Symbol, int> Transitions { get; } = transitions;
    }

    /// <summary>
    /// Represents a set of LR(0) items. This is a transparent wrapper around a
    /// <see cref="HashSet{T}"/> that provides structural equality semantics.
    /// </summary>
    /// <param name="items">The set of items. Do not modify after creation.</param>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(FlatCollectionProxy<Lr0Item, KernelItemSet>))]
    private readonly struct KernelItemSet(HashSet<Lr0Item> items) : IEquatable<KernelItemSet>, IReadOnlyCollection<Lr0Item>
    {
        // TODO-PERF: Could this be a List instead?
        private readonly HashSet<Lr0Item> _items = items;

        public int Count => _items.Count;

        public HashSet<Lr0Item>.Enumerator GetEnumerator() => _items.GetEnumerator();

        IEnumerator<Lr0Item> IEnumerable<Lr0Item>.GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(KernelItemSet other) => _items.SetEquals(other._items);

        public override bool Equals(object? obj) => obj is KernelItemSet x && Equals(x);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Count);
            foreach (var item in _items)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private readonly struct Lr0Item(Production production, int dotPosition) : IEquatable<Lr0Item>
    {
        public Production Production { get; } = production;

        public int DotPosition { get; } = dotPosition;

#if DEBUG
        private string DebuggerDisplay => Production.GetDebuggerDisplay(DotPosition);
#else
        private string DebuggerDisplay => $"Production {Production.Index} @ {DotPosition}";
#endif

        public bool Equals(Lr0Item other) => Production.Equals(other.Production) && DotPosition == other.DotPosition;

        public override bool Equals(object? obj) => obj is Lr0Item x && Equals(x);

        public override int GetHashCode() => HashCode.Combine(Production, DotPosition);

        public static implicit operator Lr0Item(Production production) => new(production, 0);
    }
}
