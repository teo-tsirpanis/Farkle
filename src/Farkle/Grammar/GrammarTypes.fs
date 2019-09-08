// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Collections
open System
open System.Collections
open System.Collections.Immutable
open System.Diagnostics

[<Struct; RequireQualifiedAccess>]
/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

[<Struct; RequireQualifiedAccess>]
/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

[<CustomComparison; CustomEquality>]
/// A symbol which is produced through a DFA, and is significant for the grammar.
type Terminal = Terminal of index: uint32 * name: string
with
    /// The terminal's index. Terminals with the same index are considered equal.
    member x.Index = match x with | Terminal (idx, _) -> idx
    /// The symbol's name.
    member x.Name = match x with | Terminal (_, name) -> name
    interface IEquatable<Terminal> with
        member x.Equals(x') = x.Index = x'.Index
    interface IComparable<Terminal> with
        member x.CompareTo(x') = compare x.Index x'.Index
    interface IStructuralEquatable with
        member x.Equals(x', _) = x.Equals(x')
        member x.GetHashCode(_) = x.GetHashCode()
    interface IStructuralComparable with
        member x.CompareTo(x', _) = compare x.Index (x' :?> Terminal).Index
    override x.Equals(x') =
        match x' with
        | :? Terminal as x' -> x.Index = x'.Index
        | _ -> false
    override x.GetHashCode() = hash x.Index
    override x.ToString() =
        let name = x.Name
        // The symbol's name should be quoted if...
        let shouldAddQuotes =
            // (this line ensures the next will not die; terminals with an empty name do not make sense)
            String.IsNullOrEmpty name
            // its first character is not a letter...
            || not <| Char.IsLetter name.[0]
            // or has a character that is not a letter, a dot, a dash, or an underscore.
            // (De Morgan's laws apply here)
            || not <| String.forall (fun x -> Char.IsLetter x || x = '.' || x = '-' || x = '_') name
        if name = "'" then
            "''"
        elif shouldAddQuotes then
            sprintf "'%s'" name
        else
            name

[<CustomComparison; CustomEquality>]
/// A symbol which is produced by a concatenation of other `Terminal`s and `Nonterminal`s, as the LALR parser dictates.
type Nonterminal = Nonterminal of index: uint32 * name: string
with
    /// The terminal's index. Nonterminals with the same index are considered equal.
    member x.Index = match x with | Nonterminal (idx, _) -> idx
    /// The nonterminal's name.
    member x.Name = match x with | Nonterminal (_, name) -> name
    interface IEquatable<Nonterminal> with
        member x.Equals(x') = x.Index = x'.Index
    interface IComparable<Nonterminal> with
        member x.CompareTo(x') = compare x.Index x'.Index
    interface IStructuralEquatable with
        member x.Equals(x', _) = x.Equals(x')
        member x.GetHashCode(_) = x.GetHashCode()
    interface IStructuralComparable with
        member x.CompareTo(x', _) = compare x.Index (x' :?> Nonterminal).Index
    override x.Equals(x') =
        match x' with
        | :? Nonterminal as x' -> x.Index = x'.Index
        | _ -> false
    override x.GetHashCode() = hash x.Index
    override x.ToString() = sprintf "<%s>" x.Name

/// A symbol which is produced through a DFA, but is not significant for the grammar and is discarded.
/// An example of a noise symbol would be a source code comment.
type Noise = Noise of name: string
with
    /// The symbol's name.
    member x.Name = match x with | Noise (name) -> name
    override x.ToString() = sprintf "(%s)" x.Name

/// A symbol signifying the end of a group.
type GroupEnd = GroupEnd of name: string
with
    /// The symbol's name.
    member x.Name = match x with | GroupEnd (name) -> name
    override x.ToString() = sprintf "(%s)" x.Name

/// A symbol signifying the start of a group.
type GroupStart = GroupStart of name: string * groupIndex: uint32
with
    /// The symbol's name.
    member x.Name = match x with | GroupStart (name, _) -> name
    override x.ToString() = sprintf "(%s)" x.Name

/// A lexical group.
/// In GOLD, lexical groups are used for situations where a number
/// of recognized tokens should be organized into a single "group".
/// This mechanism is most commonly used to handle line and block comments.
/// However, it is not limited to "noise", but can be used for any content.
and Group = {
    /// The name of the group.
    Name: string
    /// The symbol that represents the group's content.
    ContainerSymbol: Choice<Terminal, Noise>
    /// The symbol that represents the group's start.
    Start: GroupStart
    /// The symbol that represents the group's end.
    // As the GOLD Parser's site says, groups can end with normal terminals as well.
    End: Choice<Terminal, Noise, GroupEnd>
    /// The way the group advances.
    AdvanceMode: AdvanceMode
    /// The way the group ends.
    EndingMode: EndingMode
    /// A set of indexes whose corresponding groups can be nested inside this group.
    Nesting: ImmutableHashSet<uint32>
}
with
    /// Whether this group is ended by a new line.
    member x.IsEndedByNewline =
        // There are three kinds of groups in GOLD Parser.
        match x.End with
        // The group is ended by a newline, in a grammar where newlines are significant.
        | Choice1Of3 _terminal -> true
        // The group is ended by a newline, in a grammar where newlines are insignificant.
        | Choice2Of3 _noise -> true
        // The group is a block group which is ended by a group end symbol.
        // Experiments with GOLD Parser have shown that group end symbols are literals,
        // i.e. an arbitrary number cannot end a group.
        // A way to cheese GOLD Parser is to redefine a newline as something else. However,
        // it is very absurd and counterintuitive, and we cannot use actual line groups.
        // And if we redefine NewLine, we have to redefine Whitespace as well!
        // Allowing to redefine NewLine is a bad idea, but accepting all three possible
        // group end symbols is a strategy Farkle.Builder should follow. We just must not allow
        // the user to arbitrarily redefine NewLine.
        | Choice3Of3 _groupEnd -> false
    override x.ToString() = x.Name

/// A symbol that can be yielded by the DFA.
type DFASymbol = Choice<Terminal, Noise, GroupStart, GroupEnd>

/// Functions to work with `DFASymbol`s.
module DFASymbol =

    /// Converts a `DFASymbol` to a string.
    let toString: DFASymbol -> _ =
        function
        | Choice1Of4 term -> term.ToString()
        | Choice2Of4 noise -> noise.ToString()
        | Choice3Of4 gStart -> gStart.ToString()
        | Choice4Of4 gEnd -> gEnd.ToString()

/// A DFA state. It defines the logic that produces tokens out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
[<DebuggerDisplay("{Index}")>]
type DFAState = {
    /// The index of the state in the DFA state table.
    Index: uint32
    /// The edges of the state, that match a character to a next state, using a custom data structure.
    Edges: RangeMap<char, uint32>
    /// Whether this state accepts a symbol or not.
    AcceptSymbol: DFASymbol option
}

type LALRSymbol = Choice<Terminal, Nonterminal>

/// A sequence of `Terminal`s and `Nonterminal`s that can produce a specific `Nonterminal`.
type Production = {
    /// The index of the production.
    Index: uint32
    /// The `Nonterminal` the production is referring to.
    // Storing the map's key (the nonterminal) inside its value (this production)
    // is acceptable, because the production's head is an integral part of its definition.
    Head: Nonterminal
    /// The `Terminals`s and `Nonterminal`s, the production is made of.
    Handle: LALRSymbol ImmutableArray
}
with
    override x.ToString() =
        x.Handle
        |> Seq.map (function | Choice1Of2 x -> string x | Choice2Of2 x -> string x)
        |> String.concat " "
        |> sprintf "%O ::= %s" x.Head

/// An action to be taken by the LALR parser according to the given `Terminal`.
[<RequireQualifiedAccess>]
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`.
    | Shift of uint32
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and parsing ends.
    | Accept
    override x.ToString() =
        match x with
        | Shift x -> sprintf "Shift to state %d" x
        | Reduce x -> sprintf "Reduce production %O" x
        | Accept -> "Accept"

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
and LALRState = {
    /// The index of the state.
    Index: uint32
    /// The available next `LALRAction`s of the state, depending on the next `Terminal` encountered.
    Actions: ImmutableDictionary<Terminal, LALRAction>
    /// The available `LALRAction` to be taken if input ends.
    EOFAction: LALRAction option
    /// The available GOTO actions of the state.
    /// These actions are used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    GotoActions: ImmutableDictionary<Nonterminal, uint32>
}
with
    override x.ToString() = string x.Index

/// A type containing all symbols of a grammar, grouped by kind.
/// Group start and end symbols can be found at the group table.
type Symbols = {
    /// The grammar's terminals.
    Terminals: Terminal ImmutableArray
    /// The grammar's nonterminals.
    Nonterminals: Nonterminal ImmutableArray
    /// The grammar's noise symbols.
    NoiseSymbols: Noise ImmutableArray
}

/// A context-free grammar according to which, Farkle can parse text.
[<NoComparison; ReferenceEquality>]
type Grammar = internal {
    // This field is totally informative; it serves only the template maker.
    _Properties: ImmutableDictionary<string,string>

    // These fields serve the template maker again, but the information
    // they carry is redundantly stored here for his convenience.
    _StartSymbol: Nonterminal
    _Symbols: Symbols
    _Productions: Production ImmutableArray

    // These are the only fields that serve the parser.
    _Groups: Group ImmutableArray
    _LALRStates: LALRState StateTable
    _DFAStates: DFAState StateTable
}
with
    /// Metadata about the grammar. See the [GOLD Parser's documentation for more](http://www.goldparser.org/doc/egt/index.htm).
    member x.Properties = x._Properties
    /// The grammar's start `Nonterminal`.
    member x.StartSymbol = x._StartSymbol
    /// The grammar's terminals =, nonterminals, and noise symbols.
    member x.Symbols = x._Symbols
    /// The grammar's `Production`s.
    member x.Productions = x._Productions
    /// The grammar's `Group`s.
    member x.Groups = x._Groups
    /// The grammar's LALR state table.
    member x.LALRStates = x._LALRStates
    /// The grammar's DFA state table.
    member x.DFAStates = x._DFAStates
