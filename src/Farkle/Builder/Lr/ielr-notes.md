# Notes on the IELR(1) algorithm

## Introduction

Ever since it can build its own grammars, Farkle has used the LALR(1) algorithm to build parsing tables. This algorithm is a good compromise between supporting a wide range of grammars and generating small tables (compared to canonical LR(1) which is the most powerful of its kind, but generates huge tables). However there still are grammars that LALR cannot handle and raises conflicts, necessitating to rewrite the grammar, and affecting developer productivity.

There is another algorithm, [IELR(1)][ielr], that provides the full power of canonical LR(1) while generating tables of the same size as LALR(1) or slightly bigger. I have been aware of this algorithm for many years and since them I am trying to understand it and implement it in Farkle. I even have a printed copy of the algorithm's paper which sits in my desk at the time of writing this document. There are several reasons this algorithm has eluded me for so long:

1. The paper is quite mathematically dense and it is not immediately apparent which parts are necessary to implement the algorithm.
2. The paper's builds on top of an implementation of LALR that is different from how Farkle's, which was taken from the [Dragon Book]. This means that before learning how to implement IELR, I have to re-learn how to implement LALR.
3. Existing implementations of LALR cannot be relied upon because of licensing differences.
4. I have not dedicated enough time over the years to concentrate to understand the paper (it's changing recently!).

This document will contain notes in plain language about how IELR works, to help myself and potentially others. It is not beginner-friendly; some concepts are presented without explanation and it is assumed that readers are familiar with context-free grammars, and LALR(1).

> [!IMPORTANT]
> While I hope that these notes will be helpful to understand IELR, prose is not the way to precisely describe algorithms. Do not try to implement the algorithm from these notes. Use the [IELR paper][ielr] instead.[^lamport]

## The algorithm

Abstractly speaking, the way IELR works is by first trying to build LALR(1) tables and if there are conflicts, surgically adding new states to resolve them.

Farkle 7.0 will most likely not implement IELR. However, its LALR implementation will be written based on the IELR paper, in preparation for implementing IELR in a subsequent version of Farkle 7.x.

### Phase 0: LALR(1)

#### Compute LR(0) item sets

This is simple and can use the same algorithm as Farkle 6.

#### Compute goto follows

This is where things start to change. To compute reduction lookaheads, we have to compute _goto follows_. A goto follows set is the set of terminals that can appear after a goto transition is taken.

Goto follows can form when you move to the goto's destination state and take all terminals that have a shift action. Let's call these _direct goto follows_. But we can also propagate goto follows from one goto to another, in two different ways:

* There is a _successor dependency_ between two gotos, if the first goto directly leads to the state of the second goto, and the nonterminal that triggers the first goto is nullable.
* There is an _includes dependency_ between two gotos, if:
    * The nonterminal that triggers the second goto is the same as the nonterminal at the head of the first goto's item.
    * By following the symbols before the dot in the first goto's item, you can go from the second goto's state to the first goto's state.
    * The sequence of symbols one position after the dot in the first goto's item is nullable.
    > Includes dependencies are further divided into _internal_ and _predecessor_ dependencies, depending on whether the two gotos are on the same state or not.

In a dependency between two gotos, the goto follows of the second goto flow to the first goto. This can also happen recursively, but beware that after following a successor dependency, we cannot follow an includes dependency. Here's a way to compute goto follows that obeys this rule.

> This is for illustrative purposes; we will actually use [a different way](#goto-follows-via-always-follows).

1. Compute the _successor follows_ of each goto. This is the set of direct goto follows that are propagated only with successor dependencies.
2. Compute the goto follows by propagating the successor follows with includes dependencies.

#### Compute item lookaheads

Now that we have computed the goto follows, we can compute the lookahead sets of each item.

Lookahead sets form on a non-kernel item from the goto follows of the goto that created the item. They then propagate to successor items, by following the item's transition (shift or goto).

### Phase 1: Compute Auxiliary Tables

#### Predecessors

This is simple. For each state, compute the set of its immediate predecessors.

#### Goto follows from kernel items

For each goto, and for each kernel item in the goto's state, this relation holds if:

* You can go from the goto to the goto of the kernel item, by following zero or more internal dependencies.
* The sequence of symbols one position after the dot in the kernel item is nullable.

> I am 99% sure that a goto on a kernel item has this property in the symbols after the kernel item's dot are nullable, and that it propagates with internal dependencies. If this is true, then it becomes surprisingly simple to compute.

#### Always follows

For each goto, its always follows set is the set of direct goto follows that are propagated with either successor _or internal_ dependencies.

#### Goto follows via always follows

Always follows sets are needed themselves for later phases of IELR, but we can save time and use them to compute the general goto follows as well. The alogrithm above is changed like this:

```diff
- 1. Compute the successor follows of each goto. This is the set of direct goto follows that are propagated only with successor dependencies.
- 2. Compute the goto follows by propagating the successor follows with includes dependencies.
+ 1. Compute the goto follows by propagating the always follows with includes dependencies.
```

By moving this computation to phase 0, we don't have to compute the successor follows at all. This is what we will do in Farkle 7.0 even for LALR.

### Phase 2: Compute Annotations

> [!NOTE]
> This section was written by a large language model, after being fed the IELR paper and the notes from previous phases. A human review and rewrite are pending.

Phase 2's job is to find every conflict in the LALR(1) tables, trace each conflict backwards through predecessor states, and annotate the visited states with information about how they contribute to the conflict. Phase 3 will later use these annotations to decide which states need to be split.

#### Inadequacy lists

First, phase 2 identifies all conflicts in the LALR(1) tables. For each conflicted state, it records each conflict as an _inadequacy manifestation description_: a tuple of the conflicted state, the conflicted token, and the list of contributions (shift or reduce actions) that conflict on that token.

#### Item lookahead sets (on demand)

The efficient LALR(1) data structure does not store item lookahead sets (it only stores reduction lookahead sets). But phase 2 needs to trace conflicted tokens along the detailed lookahead propagation paths. To do this, it computes item lookahead sets _on demand_ for the specific kernel items that lie on the propagation paths of conflicted tokens, and caches them.

A kernel item's lookahead set is computed recursively:

* If the dot is past position 1 (i.e. the item is not at the beginning of the RHS), the lookahead set comes from the same item with the dot one position to the left in each predecessor state.
* If the dot is at position 2, the lookahead set comes from the goto follows of the gotos on the item's LHS nonterminal in each predecessor state. This is where the recursion bottoms out, using the goto follows already computed in phase 0.

#### Annotation lists

For each conflict, phase 2 traces backwards along all lanes leading to the conflicted state and attaches _inadequacy annotations_ to each visited state. An annotation is a pair of an inadequacy manifestation description and an _inadequacy contribution matrix_.

The contribution matrix has one row per conflict contribution (shift or reduce action) and one column per kernel item in the annotated state. Each row can be in one of three states:

* **Undefined (always contribution):** Any isocore that might be split from this state is _guaranteed_ to make this contribution, regardless of its kernel item lookahead sets. This happens for shift contributions (which depend only on the core, not lookaheads), or for reduce contributions from empty-RHS productions whose conflicted token is in the goto's _always follows_ set.
* **A Boolean sequence (potential or never contribution):** For each kernel item, the Boolean is true if that kernel item's lookahead set contains the conflicted token _and_ the goto follows of the relevant goto depend on that kernel item (via `follow_kernel_items`). An isocore split from this state makes this contribution if and only if at least one true-flagged kernel item's lookahead set contains the conflicted token in the isocore.
    * If at least one Boolean is true, the contribution is a _potential contribution_ — some isocores will make it, some won't.
    * If all Booleans are false, it is a _never contribution_ — no isocore can ever make it.

##### Annotating conflicted states (`annotate_manifestation`)

For the conflicted state itself:

* Each shift contribution gets an undefined (always) row — shifts depend on the core, not lookaheads.
* Each reduce contribution from a non-empty RHS production gets a Boolean row with a single true entry at the kernel item whose core matches the completed item.
* Each reduce contribution from an empty RHS production is handled by `compute_lhs_contributions`: if the conflicted token is in the always follows of the goto on the production's LHS, the row is undefined (always); otherwise the row's Booleans are set based on `follow_kernel_items` and whether the conflicted token appears in each kernel item's lookahead set.

##### Annotating predecessor states (`annotate_predecessor`)

Phase 2 then walks backwards from each annotated state to its predecessors, computing a new annotation for each predecessor. For each contribution row in the successor's matrix:

* If the row was already undefined (always), it stays undefined in the predecessor.
* Otherwise, for each kernel item in the successor that has a true Boolean:
    * If the dot in that kernel item is past position 2, we look for the matching kernel item (same production, dot one position left) in the predecessor, and check whether the conflicted token is in that predecessor kernel item's lookahead set.
    * If the dot is at position 2, the lookahead came from a goto follow. We call `compute_lhs_contributions` for the predecessor on the kernel item's LHS nonterminal. If that returns undefined (the token is in always follows), the whole row becomes undefined. Otherwise, we take the Booleans from `compute_lhs_contributions`.

This backward iteration continues until either:

1. We reach a state with no predecessors (the start state), or
2. We compute an annotation that is identical to one the state already has — iterating further would only produce duplicates. This condition guarantees termination.

#### Split-stable dominant contributions (optimization)

An annotation is _useless_ if its contribution matrix specifies a _split-stable dominant contribution_. This means that no matter how we split the annotated state into isocores, they would all make the same dominant contribution to the conflict. When this happens, splitting this state cannot help eliminate the inadequacy.

The simplest case: if every row in the matrix is either undefined (always) or all-false (never), then the set of contributions is fixed for all possible isocores, so the dominant contribution cannot change — it is split-stable.

More generally, split-stable dominance depends on the conflict resolution function Δ. Even if some contributions are potential (might or might not be present), if removing them never changes which contribution Δ selects as dominant, the annotation is still useless.

When phase 2 computes a useless annotation, it can discard it and stop iterating along that lane. This is an important optimization: for example, if a grammar uses no precedence/associativity declarations and a S/R conflict always resolves to shift, then the shift is always the dominant contribution regardless of which reduces are present, making annotations along the entire lane useless. Phase 3 would then have no reason to split any states.

### Phase 3: Split States

> [!NOTE]
> This section was written by a large language model, after being fed the IELR paper and the notes from previous phases. A human review and rewrite are pending.

Phase 3 recomputes the parser states, using the LALR(1) state cores from phase 0 as a skeleton, but with a stricter state compatibility test informed by phase 2's annotations. The result is a set of states where all LR(1)-relative inadequacies have been eliminated.

#### How it works (high level)

Phase 3 works similarly to the LR(0) construction from phase 0 step 1: it computes successor states recursively starting from the start state, and along the way merges each new state with an existing isocore if they pass a compatibility test. The key differences from LALR(1) are:

* LALR(1) merges all isocores unconditionally. Phase 3 uses a stricter test based on phase 2's annotations.
* Phase 3 does not recompute state cores or transitions — it reuses the ones from the LALR(1) tables.
* Phase 3 does not compute full lookahead sets. It only propagates the _filtered_ lookaheads that appear in phase 2's annotations, since those are the only ones that influence the compatibility test.

#### Tracking isocores

Phase 3 maintains several bookkeeping tables:

* **`lalr1_isocores`**: For each state, the index of its original LALR(1) state. New states created by splitting get the same LALR(1) isocore index as the state they were split from. This is important because the annotations and goto tables from phases 0–2 are indexed by the original LALR(1) state indices.
* **`isocore_nexts`**: A circularly linked list connecting all isocores of each LALR(1) state, so phase 3 can quickly iterate through them when searching for a compatible merge target.
* **`lookaheads_recomputed`**: A Boolean per state, initially false. Set to true once phase 3 has propagated lookaheads from at least one predecessor. A state whose lookaheads have not been recomputed is always considered compatible (it's just a placeholder waiting for its first set of lookaheads).

#### Lookahead propagation

Phase 3 does not propagate all lookaheads — only the ones that matter for the compatibility test. It uses two helper concepts:

* **`lookahead_set_filters`**: For each kernel item in a state, the set of tokens that appear in any annotation on that state's LALR(1) isocore as part of a potential contribution depending on that kernel item. Only these tokens are propagated.
* **`propagate_lookaheads`**: Computes the filtered lookaheads to propagate from a state to a given successor. For each kernel item in the successor:
    * If the dot is past position 2, the lookahead comes from the matching kernel item (same production, dot one position left) in the predecessor, intersected with the successor's filter.
    * If the dot is at position 2, the lookahead comes from a recomputed goto follow set for the predecessor, intersected with the successor's filter. The goto follow set is computed using `always_follows` plus contributions from `follow_kernel_items` and the predecessor's own `item_lookahead_sets`.

#### State compatibility test

Phase 3 considers two isocores compatible (`is_compatible`) if any of the following is true:

* The target state's lookaheads have not been recomputed yet (it's a fresh placeholder).
* For _every_ annotation on their shared LALR(1) isocore, one of the following holds:
    1. Both states make the same dominant contribution to the referenced inadequacy.
    2. One or both states make _no_ contributions to the referenced inadequacy (the conflict is irrelevant from their perspective).

The dominant contribution is computed by `dominant_contribution`: given a state, an annotation, and a set of lookaheads, it assembles the set of contributions the state would make (always contributions are always included; potential contributions are included only if the conflicted token appears in the relevant kernel item lookahead sets), and then applies Δ to select the dominant one.

The key insight is: if two isocores make the same dominant contribution, merging them cannot change the dominant contribution (assuming Δ is _merge-stable_), so the merge is safe — it cannot introduce a mysterious conflict.

#### Merge stability

The compatibility test assumes that Δ is _merge-stable_: if two subsets of contributions independently select the same dominant contribution, their union also selects that same dominant contribution. Bison's Δ is always merge-stable. If a different implementation's Δ is not merge-stable, adjustments to phases 2, 3, and 5 are needed (see section 3.5.3 of the paper for details).

#### The algorithm (`split_states`)

The algorithm is a breadth-first iteration over all states:

1. Initialize: mark all LALR(1) states as having unrecomputed lookaheads.
2. For each state _s_ (in order, including newly created states), for each transition from _s_ to a successor _s'_:
    * Compute the filtered lookaheads _K_ to propagate from _s_ to _s'_.
    * Search the isocore list of _s'_ for a compatible state _i_ (using `is_compatible(i, K)`).
    * If no compatible isocore exists: create a new state (a copy of _s'_'s LALR(1) isocore), set its lookaheads to _K_, add it to the isocore list, and redirect the transition from _s_ to point to it.
    * If _s'_ itself hasn't had its lookaheads recomputed yet: set _s'_'s lookaheads to _K_ and mark it as recomputed.
    * Otherwise: redirect the transition to point to the compatible isocore _i_, and merge _K_ into _i_'s existing lookaheads. If _K_ adds any new tokens, recursively propagate the updated lookaheads to _i_'s successors (via `merge_lookaheads`).

The recursive propagation in `merge_lookaheads` is important: when new lookaheads are merged into a state, they must be pushed forward to all successors whose lookaheads have already been computed, because the new tokens might affect compatibility tests further down the lane. The recursion stops when it encounters a successor whose lookaheads haven't been recomputed yet (those will be handled later by the main loop).

#### Suboptimum state merging

Phase 3 is a greedy algorithm — it merges with the first compatible isocore it finds. This is locally optimal but not necessarily globally optimal. Three sources of suboptimality exist:

* **Phase 3 orphans**: When a state is redirected to a different isocore, the old isocore might retain lookaheads that no longer have a source. These orphaned lookaheads can cause unnecessary compatibility failures.
* **Phase 5 orphans**: Conflict resolution in phase 5 can remove transitions, orphaning lookaheads or annotations that phases 2–3 relied on.
* **Greedy merging**: The first compatible merge found might not lead to the smallest overall table. A globally optimal solution would require considering the effects of each merge on all future merges across entire lanes.

In practice, these issues rarely matter — IELR(1) generates tables nearly as small as LALR(1) for real-world grammars. Unreachable states (from orphans) can be cleaned up in phase 6.

### Phase 4: Compute Reduction Lookaheads

This is simple. Compute the reduction lookaheads for the state table produced by phase 3.

### Phase 5: Resolve Remaining Conflicts

Use Farkle's standard conflict resolution mechanism (P&A[^pna]) to resolve any remaining conflicts.

### Phase 6: Remove Unreachable States (optional)

While this phase was not mentioned in the list of phases in section 3.1, it was mentioned in passing in section 3.8.1. Previous phases might leave some states unreachable, and we can remove them to reduce the size of the tables.

[ielr]: https://www.sciencedirect.com/science/article/pii/S0167642309001191
[Dragon Book]: https://en.wikipedia.org/wiki/Compilers:_Principles,_Techniques,_and_Tools
[^lamport]: [Quote by Leslie Lamport](https://lamport.azurewebsites.net/pubs/pubs.html#:~:text=Prose%20is%20not%20the%20way%20to%20precisely%20describe%20algorithms.%C2%A0%20Do%20not%20try%20to%20implement%20the%20algorithm%20from%20this%20paper.)
[^pna]: Precedence & Associativity
