// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.OperatorPrecedence

open System

// Let write some design notes about how Farkle implements operator precedence
// and associativity (P&A). The main challenge with implementing P&A in Farkle
// is to make it composable.

// As a sidenote, the term "operator" applies to any object that has P&A:
// terminals and productions.

// Encoding P&A info in each symbol with a number for precedence is the simplest
// solution but very ugly. What if I have an operator with a precedence of 3,
// another with precedence of 4 and want to add a third one with its precedence
// between them? I would have to renumber all operators with precedence larger or
// equal to 4. It's time-consuming and prone to errors. Developers would resort
// to BASIC-era hacks of setting precedences with multiples of ten to make their
// code resillient to such changes.
// Another unacceptable solution is to embed P&A in the metadata of the starting
// designtime Farkle, like comments. P&A will be encoded as a list of lists, with
// the outer containing, in increasing order of precedence, groups of symbols with
// the same P&A.
// This still doesn't solve the composability problem. Using a designtime Farkle with
// P&A in another one will discard them. How can the P&A of operators from many designtime
// Farkles coexist? Designtime Farkles are designed to be composable; theoretically
// one could use another from a third-party library, which has its own operator
// P&A specified. Does it always mean that two symbols with the same precedence
// actually have the same precedence?

// To fix this we will make another exception to the "metadata changes apply to
// the top level designtime Farkle" rule. During building, we will gather all P&A
// info and separate them into operator groups. Precedence between two operatos will
// be only valid if both belong to the same group.
// An important limitation is that an operator can belong in only one group. Allowing
// operators to belong to more than one can result in operators having both greater and
// smaller precedence at the same time. This limitation is also consistent with the black
// box nature of designtime Farkles. If you are returning a nonterminal, the recieving code
// should not care about its members.

// Another thing to notice is that this API does not know anything about designtime Farkles!
// In fact, the operator precedence source files are higher than the designtime Farkle
// definition files in the project. Individual symbols are identified by plain .NET objects.
// Let's be more specific:
// Terminals are identified by the designtime Farkle object with which they were used (using 
// the same terminal with different names will cause trouble but it was already discouraged).

// Literals are identified by the string of their content. The conflict resolver needs to
// know whether the grammar is case-sensitive to properly compare them.

// Productions pose a problem. Passing the production object will make the API very ugly,
// requiring storing them at a local, while they are usually created and passed inline.
// To keep the API easy to use, productions will be identified by an arbitrary object's
// reference. After a production builder is finished, we will be able to pass this object
// (or in C#'s case let Farkle create it and return it to us via an `out var`) to the
// finished production. That same object will be also passed in the operator group.
// Additional infrastructure will be provided in production builders, to cater for the
// untyped API.

// The conflict resolver will accept: the list of the operator groups, two read-only
// associative arrays that get the corresponding object of a terminal or production
// respectively, and a boolean flag indicating the grammar's case-sensitivity (for literals).

// If the resolver cannot resolve a conflict, Farkle will fail the build with an error.
// Tools like FsYacc try to automatically resolve conflicts by preferring the earliest
// production in order of appearance but Farkle has no such concept. Besides, Farkle's
// approach is to avoid such automatic action that might introduce behavior contrary to
// the developer's will.

// A final lingering issue is whether to allow resolving Reduce-Reduce conflicts using P&A.
// Neither FsYacc nor Bison do it. It might seem an interesting innovation of Farkle but the
// impact of such feature is not known. I am afraid of Reduce-Reduce conflicts being resolved
// inadvertently without the developer's notice. A Reduce-Reduce Conflict is generally more
// serious than a Shift-Reduce one. Out of abundance of caution, resolving Reduce-Reduce conflicts
// will be an opt-in behavior that can be enabled per operator group.

/// An associativity group's type. It determines the course of action in
/// case of Shift-Reduce conflicts between symbols with the same precedence.
[<RequireQualifiedAccess>]
type AssociativityType =
    /// The group's symbols are non-associative. Shift-Reduce
    /// conflicts will be resolved in favor of neither, failing
    /// with a syntax error at parse time.
    | NonAssociative
    /// The group's symbols are left-associative. Shift-Reduce
    /// conflicts will be resolved in favor of Reduce.
    | LeftAssociative
    /// The group's symbols are right-associative. Shift-Reduce
    /// conflicts will be resolved in favor of Shift.
    | RightAssociative
    /// Thr group's symbols have no associativity; only precedence.
    /// Shift-Reduce conflicts will not be resolved and will fail the build.
    | PrecedenceOnly

/// <summary>A group of symbols that have the same associativity and precedence.
/// This class and its descendants accept arrays of objects that correspond
/// to symbols.</summary>
/// <remarks><para>Terminals correspond to their designtime Farkle, literals
/// correspond to their content as a string and productions correspond to
/// the object passed or returned from the <c>WithPrecedence</c> family
/// of functions.</para>
/// <para>Using the same terminal with multiple designtime Farkles
/// (such as the original and a renamed one) causes undefined behavior
/// and will most likely result in errors.</para></remarks>
type AssociativityGroup(assocType: AssociativityType, symbols: obj seq) =
    let symbols = List.ofSeq symbols
    new (assocType, [<ParamArray>] symbols) = AssociativityGroup(assocType, List.ofArray symbols)

    /// The group's symbols' associativity.
    member _.AssociativityType = assocType
    member internal _.Symbols = symbols

/// A shortcut for creating non-associative groups.
type NonAssociative([<ParamArray>] symbols) =
    inherit AssociativityGroup(AssociativityType.NonAssociative, List.ofArray symbols)

/// A shortcut for creating left-associative groups.
type LeftAssociative([<ParamArray>] symbols) =
    inherit AssociativityGroup(AssociativityType.LeftAssociative, List.ofArray symbols)

/// A shortcut for creating right-associative groups.
type RightAssociative([<ParamArray>] symbols) =
    inherit AssociativityGroup(AssociativityType.RightAssociative, List.ofArray symbols)

/// A shortcut for creating associativity groups with only precedence and no associativity between them.
type PrecedenceOnly([<ParamArray>] symbols) =
    inherit AssociativityGroup(AssociativityType.PrecedenceOnly, List.ofArray symbols)

/// <summary>A group of associativity groups sorted by precedence.</summary>
/// <remarks><para>A symbol in an operator group has higher precedence than
/// another one if it appears in an associativity group below the former
/// symbol's associativity group.</para>
/// <para>If the same symbol is specified in multiple associativity groups,
/// it will have the precedence of the earliest group in which it appeared.</para>
/// <para>Symbols from multiple operator groups cannot be compared for precedence.</para>
/// <para>A symbol can belong in only one operator group; if it belongs in more,
/// the operator group to which the symbol will be assigned is undefined.</para>
/// <para>Operator groups are used to automatically resolve Shift-Reduce conflicts.
/// Resolving Reduce-Reduce conflicts can also happen but it must be explicitly
/// opt-in by passing a boolean argument of <see langword="true"/> in the first
/// argument of the appropriate operator group's constructor overloads.</para></remarks>
type OperatorGroup(resolvesReduceReduceConflicts, assocGroups: AssociativityGroup seq) =
    let assocGroups = List.ofSeq assocGroups
    new (resolveReduceReduceConflicts, [<ParamArray>] assocGroups) =
        OperatorGroup(resolveReduceReduceConflicts, List.ofArray assocGroups)
    new ([<ParamArray>] assocGroups) = OperatorGroup(false, List.ofArray assocGroups)

    /// Whether Farkle uses this operator group to automatically resolve
    /// Reduce-Reduce conflicts. Because the impact of this feature is
    /// unknown, it is set to false by default. It can be changed by
    /// passing true to a constructor overload that accepts a boolean.
    member _.ResolvesReduceReduceConflict = resolvesReduceReduceConflicts
    member internal _.AssociativityGroups = assocGroups
