// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BitCollections;
using Farkle.Diagnostics;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars.StateMachines;
using Farkle.Grammars.Writers;
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
    /// Builds an LR(1) state machine that can parse the syntax of a grammar.
    /// </summary>
    /// <param name="syntax">The syntax of the grammar.</param>
    /// <param name="conflictResolver">The conflict resolver to use. Optional.</param>
    /// <param name="log">Used to log events in the building process.</param>
    /// <param name="cancellationToken">Used to cancel the building process.</param>
    public static LrWriter Build(IGrammarSyntaxProvider syntax, LrConflictResolver? conflictResolver = null, BuilderLogger log = default, CancellationToken cancellationToken = default)
    {
        var @this = new LalrBuild(syntax, log, cancellationToken);
        var lr0StateMachine = @this.ComputeLr0StateMachine();
        var nullableNonterminals = @this.ComputeNullableNonterminals();
        var productionNullableStarts = @this.ComputeProductionNullableStarts(nullableNonterminals);
        var gotoFollowDependencies = @this.ComputeGotoFollowDependencies(lr0StateMachine, nullableNonterminals, productionNullableStarts.AsSpan());
        var alwaysFollows = @this.PropagateGotoFollows(lr0StateMachine, gotoFollowDependencies.AsSpan(),
            GotoFollowDependencyKinds.Successor | GotoFollowDependencyKinds.Internal);
        var gotoFollows = @this.PropagateGotoFollows(lr0StateMachine, gotoFollowDependencies.AsSpan(),
            GotoFollowDependencyKinds.Internal | GotoFollowDependencyKinds.Predecessor, alwaysFollows.AsSpan());
        var reductionLookaheads = @this.ComputeReductionLookaheads(lr0StateMachine, gotoFollows.AsSpan());

        LrStateMachine stateMachine = new DefaultLrStateMachine(lr0StateMachine, reductionLookaheads);
        if (conflictResolver is not null)
        {
            stateMachine = new ConflictResolvingLrStateMachine(stateMachine, conflictResolver);
        }
        return stateMachine.ToLrWriter();
    }

    /// <summary>
    /// Computes the reduction lookahead sets of each state.
    /// </summary>
    /// <param name="stateMachine">The state machine.</param>
    /// <param name="gotoFollows">The follow sets of each GOTO transition of the state machine.</param>
    /// <returns>
    /// An array of lists for each state, with each list containing:
    /// <list type="bullet">
    /// <item><description>The production to reduce.</description></item>
    /// <item><description>A bit array of the terminals to perform the reduction on. Do not modify.</description></item>
    /// </list>
    /// If a list for a state is <see langword="null"/>, it means that no reductions happen in this state.
    /// </returns>
    private ImmutableArray<List<(Production Production, BitArrayNeo Lookahead)>?> ComputeReductionLookaheads(
        Lr0StateMachine stateMachine, ReadOnlySpan<BitArrayNeo> gotoFollows)
    {
        Log.Debug("Computing reduction lookaheads");
        // These variables are global to the whole process.
        ReadOnlySpan<Lr0State> states = stateMachine.States.AsSpan();
        ReadOnlySpan<GotoInfo> gotos = stateMachine.Gotos.AsSpan();
        var reductionLookaheads = new List<(Production Production, BitArrayNeo Lookahead)>?[states.Length];
        // These variables are local to each step, but we declare them
        // outside the loop and reuse them for performance.
        // ComputeLr0StateMachine tracks whole items, we can just track nonterminals because we are specifically interested in the non-kernal items produced by each kernel item.
        var nonterminalsToProcess = new Queue<int>();
        var visitedNonterminals = new BitArrayNeo(Syntax.NonterminalCount);
        for (int i = 0; i < states.Length; i++)
        {
            // The idea is, for each GOTO on a kernel item, get the non-kernel items that are
            // derived by the kernel item, and for each of them, keep moving the dot to the right
            // and when you reach the end, set the lookahead set of that item to the GOTO follow
            // set of the original kernel item's GOTO.
            ref readonly Lr0State state = ref states[i];
            foreach (Lr0Item kernelItem in state.KernelItems)
            {
                // Skip items whose dot is at the end.
                if (!TryAdvanceItem(kernelItem, out Symbol s, out _))
                {
                    continue;
                }
                // Skip items whose dot is at a terminal.
                if (s.IsTerminal)
                {
                    continue;
                }
                BitArrayNeo emergedLookaheadSet = gotoFollows[state.Transitions[s]];
                nonterminalsToProcess.Enqueue(s.Index);
                while (nonterminalsToProcess.TryDequeue(out int nonterminal))
                {
                    CancellationToken.ThrowIfCancellationRequested();

                    if (!visitedNonterminals.Set(nonterminal, true))
                    {
                        continue;
                    }

                    foreach (Production p in Syntax.EnumerateNonterminalProductions(nonterminal))
                    {
                        ProductionMemberList productionMembers = Syntax.GetProductionMembers(p);
                        // If the production starts with a nonterminal, queue it so that we can
                        // look at its own productions as well.
                        if (productionMembers is [{ IsTerminal: false, Index: int firstNonterminalOfProduction }, ..])
                        {
                            nonterminalsToProcess.Enqueue(firstNonterminalOfProduction);
                        }
                        // From the state we are, follow the production to the end.
                        int currentState = i;
                        foreach (Symbol s2 in productionMembers)
                        {
                            currentState = states[currentState].FollowTransition(s2, gotos);
                        }
                        (reductionLookaheads[currentState] ??= []).Add((p, emergedLookaheadSet));
                    }
                }

                Debug.Assert(nonterminalsToProcess.Count == 0);
                visitedNonterminals.SetAll(false);
            }
        }
        Log.Debug("Computed reduction lookaheads");
        return ImmutableCollectionsMarshal.AsImmutableArray(reductionLookaheads);
    }

    /// <summary>
    /// Computes and propagates GOTO follow sets.
    /// </summary>
    /// <param name="stateMachine">The state machine.</param>
    /// <param name="dependencies">The GOTO follow dependencies, computed by
    /// <see cref="ComputeGotoFollowDependencies"/>.</param>
    /// <param name="dependencyKindsToPropagate">The kinds of dependencies to propagate.</param>
    /// <param name="existingGotoFollows">The GOTO follow set to initialize the returning
    /// value with, before propagation. If not specified, it will generate the initial
    /// GOTO follows from the Shift transitions of each GOTO's destination states.</param>
    /// <returns>An array of bit arrays, containing the sets of terminals that can appear after
    /// each GOTO transition.</returns>
    private ImmutableArray<BitArrayNeo> PropagateGotoFollows(Lr0StateMachine stateMachine,
        ReadOnlySpan<GotoFollowDependency> dependencies, GotoFollowDependencyKinds dependencyKindsToPropagate,
        ReadOnlySpan<BitArrayNeo> existingGotoFollows = default)
    {
        Debug.Assert(existingGotoFollows.IsEmpty || existingGotoFollows.Length == stateMachine.Gotos.Length);
        var follows = ImmutableArray.CreateBuilder<BitArrayNeo>(stateMachine.Gotos.Length);
        if (existingGotoFollows.IsEmpty)
        {
            Log.Debug("Generating initial GOTO follow sets");
            foreach (ref readonly var @goto in stateMachine.Gotos.AsSpan())
            {
                var follow = new BitArrayNeo(Syntax.TerminalCount);
                foreach (Symbol s in stateMachine.States[@goto.ToState].Transitions.Keys)
                {
                    if (s.IsTerminal)
                    {
                        follow.Set(s.Index, true);
                    }
                }
                follows.Add(follow);
            }
            Log.Debug("Generated initial GOTO follow sets");
        }
        else
        {
            foreach (BitArrayNeo follow in existingGotoFollows)
            {
                follows.Add(new(follow));
            }
        }

        Log.Debug($"Propagating {dependencyKindsToPropagate} GOTO follow dependencies");
        bool changed;
        int iterations = 0;
        do
        {
            CancellationToken.ThrowIfCancellationRequested();

            changed = false;
            iterations++;
            foreach (var dependency in dependencies)
            {
                if ((dependencyKindsToPropagate & dependency.DependencyKind) != 0)
                {
                    changed |= follows[dependency.FromGoto].Or(follows[dependency.ToGoto]);
                }
            }
        } while (changed);
        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Propagated after {iterations} iterations");
            if (Log.IsEnabled(DiagnosticSeverity.Verbose))
            {
                foreach (BitArrayNeo x in follows)
                {
                    Log.Verbose($"{x}");
                }
            }
        }
        return follows.MoveToImmutable();
    }

    /// <summary>
    /// Computes the dependencies between the follow sets of GOTO transitions.
    /// </summary>
    private ImmutableArray<GotoFollowDependency> ComputeGotoFollowDependencies(Lr0StateMachine stateMachine,
        BitArrayNeo nullableNonterminals, ReadOnlySpan<int> productionNullableStarts)
    {
        Log.Debug("Computing GOTO follow dependencies");
        ReadOnlySpan<Lr0State> states = stateMachine.States.AsSpan();
        ReadOnlySpan<GotoInfo> gotos = stateMachine.Gotos.AsSpan();
        var dependencies = ImmutableArray.CreateBuilder<GotoFollowDependency>();
        int successorCount = 0, internalCount = 0, predecessorCount = 0;
        for (int i = 0; i < gotos.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ref readonly GotoInfo @goto = ref gotos[i];

            // Compute successor dependencies.
            foreach (var transition in states[@goto.ToState].Transitions)
            {
                // If GOTO A leads to a state with GOTO B, and B gets triggered by a nullable
                // nonterminal, then the follow set of A should include the follow set of B.
                if (transition.Key.IsTerminal)
                {
                    continue;
                }
                if (nullableNonterminals[gotos[transition.Value].Symbol])
                {
                    dependencies.Add(new(i, transition.Value, isSuccessor: true));
                    successorCount++;
                }
            }

            // Compute includes dependencies.
            foreach (Production p in Syntax.EnumerateNonterminalProductions(@goto.Symbol))
            {
                CancellationToken.ThrowIfCancellationRequested();

                ProductionMemberList members = Syntax.GetProductionMembers(p);
                // We have to see if a production is of the form A -> αBβ, where:
                // - α and β are potentially empty sequences of symbols
                // - B is a nonterminal
                // - β is either empty or all of its symbols are nullable
                // If so, the follow set of the GOTO on the item A -> α•Bβ that starts
                // from our GOTO's state, should include the follow set of our GOTO.
                // If β is not empty, the item can be rewritten as A -> αB•ββ', and
                // by substituting α with αB, B with β and β with β', the property still holds.

                // Skip productions with no members, or whose last member is a terminal.
                // There is no B we can take that satisfies the properties above.
                if (members is [] or [.., { IsTerminal: true }])
                {
                    continue;
                }
                int indexOfB;
                {
                    // We split the production into parts α and β and we need to take B from one of the parts.
                    // Let's keep this variable in a separate block to avoid accidents.
                    int indexOfβ = productionNullableStarts[p.Index];
                    if (indexOfβ > 0 && !members[indexOfβ - 1].IsTerminal)
                    {
                        // If α is not empty (i.e. the first element of β is not the first member of the production)
                        // and the last element of α is a nonterminal, then B is the last element of α.
                        indexOfB = indexOfβ - 1;
                    }
                    else
                    {
                        // Otherwise (i.e. α is empty or its last element is a terminal), B is the first element of β
                        // and β's start gets moved one place to the right. We can do this per above.
                        indexOfB = indexOfβ;
                    }
                }
                int state = @goto.FromState;
                int j;
                // Follow the production through α until we reach B.
                for (j = 0; j < indexOfB; j++)
                {
                    state = states[state].FollowTransition(members[j], gotos);
                }
                // We now have Bβ in front of us. Per above, each subsequent member can be substituted as B.
                // Add a dependency for each of them.
                for (; j < members.Count; j++)
                {
                    Symbol s = members[j];
                    // All these members are nonterminals, which means that all transitions are GOTOs.
                    Debug.Assert(!s.IsTerminal);
                    int gotoIdx = states[state].Transitions[s];
                    dependencies.Add(new(gotoIdx, i, isSuccessor: false));
                    state = gotos[gotoIdx].ToState;

                    // We don't specifically store whether a dependency is internal or predecessor,
                    // but we keep a count of each kind for diagnostic purposes.
                    // There are two equivalent definitions of internal dependencies. Either α is empty
                    // (i.e. B is the first member of the production) or both GOTOs of the dependency
                    // are in the same state. Assert that the former is true iff the latter is true.
                    bool isInternalDependency = indexOfB == 0;
                    Debug.Assert(isInternalDependency == (gotos[gotoIdx].FromState == i));
                    if (isInternalDependency)
                    {
                        internalCount++;
                    }
                    else
                    {
                        predecessorCount++;
                    }
                }
            }
        }
        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Computed GOTO follow dependencies: {successorCount} successors, {internalCount} internals, {predecessorCount} predecessors");
        }
        return dependencies.DrainToImmutable();
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
    private Lr0StateMachine ComputeLr0StateMachine()
    {
        Log.Debug("Computing states...");
        // These variables are global to the whole process.
        var states = ImmutableArray.CreateBuilder<Lr0State>();
        var gotos = ImmutableArray.CreateBuilder<GotoInfo>();
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

        _ = GetOrQueueItemSet(new([Syntax.StartProduction]));

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
                var destinationState = GetOrQueueItemSet(new(x.Value));
                if (x.Key.IsTerminal)
                {
                    transitions.Add(x.Key, destinationState);
                }
                else
                {
                    transitions.Add(x.Key, gotos.Count);
                    gotos.Add(new(states.Count, destinationState, x.Key.Index));
                }
            }
            states.Add(new Lr0State(kernelItems, transitions));

            Debug.Assert(itemsToProcess.Count == 0);
            itemsToProcess.Clear();
            grouppedTransitions.Clear();
        }

        if (Log.IsEnabled(DiagnosticSeverity.Debug))
        {
            Log.Debug($"Created {states.Count} states with {gotos.Count} gotos.");
        }

        return new(states.DrainToImmutable(), gotos.DrainToImmutable());

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

    private sealed class DefaultLrStateMachine(Lr0StateMachine states,
        ImmutableArray<List<(Production Production, BitArrayNeo Lookahead)>?> reductionLookaheads) : LrStateMachine
    {
        private Lr0StateMachine Lr0StateMachine { get; } = states;

        private ImmutableArray<List<(Production Production, BitArrayNeo Lookahead)>?> ReductionLookaheads { get; } = reductionLookaheads;

        public override int StateCount => Lr0StateMachine.States.Length;

        public override IEnumerable<LrStateEntry> GetEntriesOfState(int state)
        {
            foreach (var transition in Lr0StateMachine.States[state].Transitions)
            {
                switch (transition.Key)
                {
                    case { IsTerminal: true, Index: int idx }:
                        yield return LrStateEntry.Create(TranslateTerminalIndex(idx), LrAction.CreateShift(transition.Value));
                        break;
                    case { IsTerminal: false, Index: int idx }:
                        yield return LrStateEntry.CreateGoto(TranslateNonterminalIndex(idx), Lr0StateMachine.Gotos[transition.Value].ToState);
                        break;
                }
            }

            if (ReductionLookaheads[state] is not { } lookaheads)
            {
                yield break;
            }
            foreach ((var p, BitArrayNeo lookahead) in lookaheads)
            {
                if (p.Index == StartProductionIndex)
                {
                    foreach (int terminal in lookahead)
                    {
                        Debug.Assert(terminal == EndSymbolIndex);
                        yield return LrStateEntry.CreateEndOfFileAction(LrEndOfFileAction.Accept);
                    }
                }
                else
                {
                    var productionHandle = TranslateProductionIndex(p.Index);
                    foreach (int terminal in lookahead)
                    {
                        if (terminal == EndSymbolIndex)
                        {
                            yield return LrStateEntry.CreateEndOfFileAction(LrEndOfFileAction.CreateReduce(productionHandle));
                        }
                        yield return LrStateEntry.Create(TranslateTerminalIndex(terminal), LrAction.CreateReduce(productionHandle));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents the kinds of dependencies to propagate GOTO follow sets through.
    /// </summary>
    /// <seealso cref="PropagateGotoFollows"/>
    [Flags]
    private enum GotoFollowDependencyKinds
    {
        /// <summary>
        /// Do not propagate any dependencies.
        /// </summary>
        None = 0,
        /// <summary>
        /// Propagate dependencies between a GOTO and an immediate successor of it.
        /// </summary>
        Successor = 1,
        /// <summary>
        /// Propagate dependencies between two GOTOs in the same state.
        /// </summary>
        Internal = 2,
        /// <summary>
        /// Propagate dependencies between a GOTO and an eventual predecessor of it.
        /// </summary>
        Predecessor = 4
    }

    /// <summary>
    /// Represents a dependency between the follow sets of two GOTO transitions.
    /// </summary>
    private readonly struct GotoFollowDependency(int fromGoto, int toGoto, bool isSuccessor)
    {
        private const uint IsSuccessorMask = 1u << 31;

        private readonly uint _fromGotoAndIsSuccessor = (uint)fromGoto | (isSuccessor ? IsSuccessorMask : 0);

        /// <summary>
        /// The GOTO transition from which the dependency originates.
        /// </summary>
        public int FromGoto => (int)(_fromGotoAndIsSuccessor & ~IsSuccessorMask);

        /// <summary>
        /// The GOTO transition to which the dependency leads.
        /// </summary>
        public int ToGoto { get; } = toGoto;

        /// <summary>
        /// The kind of the dependency.
        /// </summary>
        public GotoFollowDependencyKinds DependencyKind
        {
            get
            {
                if ((_fromGotoAndIsSuccessor & IsSuccessorMask) != 0)
                {
                    return GotoFollowDependencyKinds.Successor;
                }
                if (FromGoto == ToGoto)
                {
                    return GotoFollowDependencyKinds.Internal;
                }
                return GotoFollowDependencyKinds.Predecessor;
            }
        }
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
        /// <remarks>
        /// IMPORTANT: If the key is a terminal, the value is an index in <see cref="Lr0StateMachine.States"/>,
        /// but if the key is a nonterminal, the value is an index in <see cref="Lr0StateMachine.Gotos"/>.
        /// </remarks>
        /// <seealso cref="FollowTransition"/>
        public Dictionary<Symbol, int> Transitions { get; } = transitions;

        /// <summary>
        /// Follows a transition from this state.
        /// </summary>
        /// <param name="symbol">The symbol whose transition to follow.</param>
        /// <param name="gotos">The GOTO transitions of the state machine,
        /// as returned by <see cref="Lr0StateMachine.Gotos"/>.</param>
        /// <returns>The index of the destination state.</returns>
        /// <remarks>This method correctly handles GOTO transitions and is
        /// recommended to be used over <see cref="Transitions"/>.</remarks>
        public int FollowTransition(Symbol symbol, ReadOnlySpan<GotoInfo> gotos)
        {
            int idx = Transitions[symbol];
            if (symbol.IsTerminal)
            {
                return idx;
            }
            return gotos[idx].ToState;
        }
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
