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

### Phase 3: Split States

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
