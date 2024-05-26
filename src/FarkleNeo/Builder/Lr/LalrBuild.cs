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
        var gotoFollows = @this.ComputeInitialGotoFollows(lr0StateMachine);
        // The rule is, after taking a successor dependency, no internal dependency can be followed.
        // We can propagate all successor dependencies first, but can also propagate internal dependencies
        // at the same time. This has an equivalent effect according to §3.3.3 of the IELR paper.
        @this.PropagateGotoFollows(lr0StateMachine, gotoFollowDependencies.AsSpan(),
            GotoFollowDependencyKinds.Successor | GotoFollowDependencyKinds.Internal, gotoFollows);
        @this.PropagateGotoFollows(lr0StateMachine, gotoFollowDependencies.AsSpan(),
            GotoFollowDependencyKinds.Internal | GotoFollowDependencyKinds.Predecessor, gotoFollows);
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
    /// An array of dictionaries for each state, with each dictionary containing:
    /// <list type="bullet">
    /// <item><description>The production to reduce.</description></item>
    /// <item><description>A bit array of the terminals to perform the reduction on. Do not modify.</description></item>
    /// </list>
    /// If a dictionary for a state is <see langword="null"/>, it means that no reductions happen in this state.
    /// </returns>
    private ImmutableArray<Dictionary<Production, BitArrayNeo>?> ComputeReductionLookaheads(
        Lr0StateMachine stateMachine, ReadOnlySpan<BitArrayNeo> gotoFollows)
    {
        Log.Debug("Computing reduction lookaheads");
        ReadOnlySpan<Lr0State> states = stateMachine.States.AsSpan();
        ReadOnlySpan<GotoInfo> gotos = stateMachine.Gotos.AsSpan();
        var reductionLookaheads = new Dictionary<Production, BitArrayNeo>?[states.Length];
        // For each GOTO in the grammar, we take its follow set and push it through the productions
        // of each non-kernel item derived by the GOTO. We don't have to actually compute the non-
        // kernel items, we can get all productions of the nonterminal that triggers the GOTO. We
        // also don't have to deal with duplicates, as the GOTO list has taken care of items where
        // the dot is on the same nonterminal.
        for (int i = 0; i < gotos.Length; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ref readonly GotoInfo @goto = ref gotos[i];
            PropagateLookaheads(in Syntax, states, gotos, @goto.FromState, @goto.NonterminalIndex, gotoFollows[i]);
        }
        // There is one more GOTO to propagate, the one on <S'> → • <S>. The loop before propagated
        // the GOTO follows on the derived productions of <S>, but did not follow the GOTO itself.
        // This is necessary to add the accept action.
        PropagateLookaheads(in Syntax, states, gotos, 0, StartSymbolIndex, gotoFollows[0]);
        Log.Debug("Computed reduction lookaheads");
        return ImmutableCollectionsMarshal.AsImmutableArray(reductionLookaheads);

        void PropagateLookaheads(in AugmentedSyntaxProvider syntax, ReadOnlySpan<Lr0State> states, ReadOnlySpan<GotoInfo> gotos,
            int state, int nonterminal, BitArrayNeo lookahead)
        {
            foreach (Production p in syntax.EnumerateNonterminalProductions(nonterminal))
            {
                int currentState = state;
                foreach (Symbol s in syntax.GetProductionMembers(p))
                {
                    currentState = states[currentState].FollowTransition(s, gotos);
                }
                var dict = reductionLookaheads[currentState] ??= [];
                if (!dict.TryGetValue(p, out BitArrayNeo? existingLookahead))
                {
                    dict.Add(p, new(lookahead));
                }
                else
                {
                    existingLookahead.Or(lookahead);
                }
            }
        }
    }

    /// <summary>
    /// Computes and propagates GOTO follow sets.
    /// </summary>
    /// <param name="stateMachine">The state machine.</param>
    /// <param name="dependencies">The GOTO follow dependencies, computed by
    /// <see cref="ComputeGotoFollowDependencies"/>.</param>
    /// <param name="dependencyKindsToPropagate">The kinds of dependencies to propagate.</param>
    /// <param name="follows">The existing GOTO follow sets that will be propagated in-place.</param>
    private void PropagateGotoFollows(Lr0StateMachine stateMachine,
        ReadOnlySpan<GotoFollowDependency> dependencies, GotoFollowDependencyKinds dependencyKindsToPropagate,
        ImmutableArray<BitArrayNeo> follows)
    {
        var gotos = stateMachine.Gotos.AsSpan();
        Debug.Assert(follows.Length == gotos.Length);

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
                if ((dependencyKindsToPropagate & dependency.GetDependencyKind(gotos)) != 0)
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
    }

    /// <summary>
    /// Computes the initial GOTO follow sets.
    /// </summary>
    /// <param name="stateMachine">The state machine.</param>
    /// <remarks>
    /// The initial follow set of a GOTO is the set of terminals that can be matched
    /// right after following the GOTO. Additional terminals may indirectly be followed
    /// as well, and they can be computed by propagating the follow sets using
    /// <see cref="PropagateGotoFollows"/>.
    /// </remarks>
    private ImmutableArray<BitArrayNeo> ComputeInitialGotoFollows(Lr0StateMachine stateMachine)
    {
        var gotos = stateMachine.Gotos.AsSpan();
        var follows = ImmutableArray.CreateBuilder<BitArrayNeo>(gotos.Length);
        Log.Debug("Generating initial GOTO follow sets");
        // The first GOTO is the one on <S'> → • <S>, and its follow set consists of only the end symbol.
        // This happens because the reducing the start production means accepting, and we can only accept
        // at the end of input.
        var initialFollow = new BitArrayNeo(Syntax.TerminalCount);
        initialFollow[EndSymbolIndex] = true;
        follows.Add(initialFollow);
        // Add the follow sets of the rest of the GOTOs.
        foreach (ref readonly var @goto in gotos[1..])
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
                if (nullableNonterminals[gotos[transition.Value].NonterminalIndex])
                {
                    dependencies.Add(new(i, transition.Value, isSuccessor: true));
                    successorCount++;
                }
            }

            // Compute includes dependencies.
            foreach (Production p in Syntax.EnumerateNonterminalProductions(@goto.NonterminalIndex))
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
                    // It's possible to have a dependency from a GOTO to itself in items like:
                    // <A> → a • <B>
                    // <B> → • b <A>
                    if (gotoIdx != i)
                    {
                        dependencies.Add(new(gotoIdx, i, isSuccessor: false));

                        // We don't specifically store whether a dependency is internal or predecessor,
                        // but we keep a count of each kind for diagnostic purposes.
                        // The most reliable indicator of an internal dependency is that both GOTOs are in
                        // the same state. The IELR paper says that an equivalent definition is that α is
                        // empty (i.e. B is the first member of the production), but this equivalence does
                        // not hold in certain cases involving recursive productions.
                        // Consider the following grammar:
                        // <A> → a <B>
                        // <B> → <C>
                        // <C> → <A>
                        // There is a dependency from the GOTO on <C> to the GOTO on <B>, but following `a`
                        // while we are on <A> → a • <B> will lead us back to the same state, while `a` is
                        // not empty.
                        bool isInternalDependency = gotos[gotoIdx].FromState == gotos[i].FromState;
                        if (isInternalDependency)
                        {
                            internalCount++;
                        }
                        else
                        {
                            predecessorCount++;
                        }
                    }
                    state = gotos[gotoIdx].ToState;
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
                bool isNullable = !s.IsTerminal && nullableNonterminals[s.Index];
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
                        // The only nullable terminal is the end symbol, which is not encountered in syntax.
                        if (s.IsTerminal)
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
                        break;
                    }
                }
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
        var kernelItemMap = new Dictionary<KernelItemSet, int>();
        // These variables are local to each step, but we declare them
        // outside the loop and reuse them for performance.
        var itemsToProcess = new Queue<Lr0Item>();
        var visitedItems = new HashSet<Lr0Item>();
        // This has to be a sorted dictionary to ensure that new states
        // are being created in a deterministic order.
        var grouppedTransitions = new SortedDictionary<Symbol, List<Lr0Item>>();

        _ = GetOrQueueItemSet(new([Syntax.StartProduction]));

        while (kernelItemSetsToProcess.TryDequeue(out var kernelItems))
        {
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
                    gotos.Add(new(states.Count, destinationState, x.Key.Index, Syntax));
                }
            }
            states.Add(new Lr0State(kernelItems, transitions));

            Debug.Assert(itemsToProcess.Count == 0);
            itemsToProcess.Clear();
            visitedItems.Clear();
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
        ImmutableArray<Dictionary<Production, BitArrayNeo>?> reductionLookaheads) : LrStateMachine
    {
        private Lr0StateMachine Lr0StateMachine { get; } = states;

        private ImmutableArray<Dictionary<Production, BitArrayNeo>?> ReductionLookaheads { get; } = reductionLookaheads;

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
                        else
                        {
                            yield return LrStateEntry.Create(TranslateTerminalIndex(terminal), LrAction.CreateReduce(productionHandle));
                        }
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
    [DebuggerDisplay("{FromGoto} → {ToGoto}, {DebuggerDependencyKind,nq}")]
    private readonly struct GotoFollowDependency
    {
        private const uint IsSuccessorMask = 1u << 31;

        private readonly uint _fromGotoAndIsSuccessor;

        public GotoFollowDependency(int fromGoto, int toGoto, bool isSuccessor)
        {
            Debug.Assert(fromGoto != toGoto);
            _fromGotoAndIsSuccessor = (uint)fromGoto | (isSuccessor ? IsSuccessorMask : 0);
            ToGoto = toGoto;
        }

        private string DebuggerDependencyKind => IsSuccessor ? "Successor" : "Include";

        /// <summary>
        /// The GOTO transition from which the dependency originates.
        /// </summary>
        public int FromGoto => (int)(_fromGotoAndIsSuccessor & ~IsSuccessorMask);

        /// <summary>
        /// The GOTO transition to which the dependency leads.
        /// </summary>
        public int ToGoto { get; }

        /// <summary>
        /// Whether the dependency is a successor dependency, otherwise it is an include dependency.
        /// </summary>
        private bool IsSuccessor => (_fromGotoAndIsSuccessor & IsSuccessorMask) != 0;

        /// <summary>
        /// Gets the exact kind of the dependency, which requires the table with the GOTOs.
        /// </summary>
        public GotoFollowDependencyKinds GetDependencyKind(ReadOnlySpan<GotoInfo> gotos)
        {
            if (IsSuccessor)
            {
                return GotoFollowDependencyKinds.Successor;
            }
            // We could keep the full kind in the struct, taking advantage of both integers'
            // high bits. It will complicate things a bit, let's not do it right now, unless
            // this proves to be a performance bottleneck.
            if (gotos[FromGoto].FromState == gotos[ToGoto].FromState)
            {
                return GotoFollowDependencyKinds.Internal;
            }
            return GotoFollowDependencyKinds.Predecessor;
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
    [DebuggerDisplay("{FromState} → {ToState} ({_symbol})")]
    private readonly struct GotoInfo(int fromState, int toState, int nonterminal, AugmentedSyntaxProvider syntax)
    {
        // Even though it's always a nonterminal, we use a full symbol to take
        // advantage of better debugger display.
        private readonly Symbol _symbol = Symbol.CreateNonterminal(nonterminal, syntax);

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
        public int NonterminalIndex => _symbol.Index;
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
    /// <see cref="List{T}"/> that provides structural equality semantics.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(FlatCollectionProxy<Lr0Item, KernelItemSet>))]
    private readonly struct KernelItemSet : IEquatable<KernelItemSet>, IReadOnlyCollection<Lr0Item>
    {
        private readonly List<Lr0Item> _items;

        /// <summary>
        /// Creates a <see cref="KernelItemSet"/>.
        /// </summary>
        /// <param name="items">The list of items. Do not modify after creation.</param>
        public KernelItemSet(List<Lr0Item> items)
        {
            items.Sort();
            _items = items;
        }

        public int Count => _items.Count;

        public List<Lr0Item>.Enumerator GetEnumerator() => _items.GetEnumerator();

        IEnumerator<Lr0Item> IEnumerable<Lr0Item>.GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(KernelItemSet other)
        {
            if (Count != other.Count)
            {
                return false;
            }
            for (int i = 0; i < Count; i++)
            {
                if (!_items[i].Equals(other._items[i]))
                {
                    return false;
                }
            }
            return true;
        }

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
    private readonly struct Lr0Item(Production production, int dotPosition) : IEquatable<Lr0Item>, IComparable<Lr0Item>
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

        public int CompareTo(Lr0Item other) =>
            (Production.Index, DotPosition).CompareTo((other.Production.Index, other.DotPosition));

        public override int GetHashCode() => HashCode.Combine(Production, DotPosition);

        public static implicit operator Lr0Item(Production production) => new(production, 0);
    }
}
