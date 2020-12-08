// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle.Collections
open System
open System.Collections.Immutable
open System.Diagnostics
open System.Runtime.CompilerServices

[<Struct; IsReadOnly; RequireQualifiedAccess>]
/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

[<Struct; IsReadOnly; RequireQualifiedAccess>]
/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

[<CustomComparison; CustomEquality>]
/// A symbol which is produced through a DFA, and is significant for the grammar.
/// Terminals with the same index are considered equal.
type Terminal = Terminal of Index: uint32 * Name: string
with
    member private x.index = match x with | Terminal (idx, _) -> idx
    member private x.name = match x with | Terminal (_, name) -> name
    interface IEquatable<Terminal> with
        member x.Equals x' = x.index = x'.index
    interface IComparable<Terminal> with
        member x.CompareTo x' = compare x.index x'.index
    interface IComparable with
        member x.CompareTo x' = compare x.index (x' :?> Terminal).index
    override x.Equals(x') =
        obj.ReferenceEquals(x, x') ||
        match x' with
        | :? Terminal as x' -> x.index = x'.index
        | _ -> false
    override x.GetHashCode() = x.index.GetHashCode()
    override x.ToString() =
        let name = x.name
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
/// A symbol which is produced by a concatenation of other `Terminal`s
/// and `Nonterminal`s, as the LALR parser dictates. Nonterminals with
/// the same index are considered equal.
type Nonterminal = Nonterminal of Index: uint32 * Name: string
with
    member x.index = match x with | Nonterminal (idx, _) -> idx
    member x.name = match x with | Nonterminal (_, name) -> name
    interface IEquatable<Nonterminal> with
        member x.Equals x' = x.index = x'.index
    interface IComparable<Nonterminal> with
        member x.CompareTo x' = compare x.index x'.index
    interface IComparable with
        member x.CompareTo x' = compare x.index (x' :?> Nonterminal).index
    override x.Equals x' =
        obj.ReferenceEquals(x, x') ||
        match x' with
        | :? Nonterminal as x' -> x.index = x'.index
        | _ -> false
    override x.GetHashCode() = x.index.GetHashCode()
    override x.ToString() = sprintf "<%s>" x.name

/// A symbol which is produced through a DFA, but is not significant for the grammar and is discarded.
/// An example of a noise symbol would be a source code comment.
type Noise = Noise of Name: string
with
    override x.ToString() = match x with Noise name -> sprintf "(%s)" name

/// A symbol signifying the end of a group.
type GroupEnd = GroupEnd of name: string
with
    /// The symbol's name.
    member x.Name = match x with | GroupEnd name -> name
    override x.ToString() = match x with GroupEnd name -> sprintf "(%s)" name

/// A symbol signifying the start of a group.
type GroupStart = GroupStart of Name: string * GroupIndex: uint32
with
    override x.ToString() = match x with GroupStart(name, _) -> sprintf "(%s)" name

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

/// A lexical group.
/// In GOLD, lexical groups are used for situations where a number
/// of recognized tokens should be organized into a single "group".
/// This mechanism is most commonly used to handle line and block comments.
/// However, it is not limited to "noise", but can be used for any content.
type Group = {
    /// The name of the group.
    Name: string
    /// The symbol that represents the group's content.
    ContainerSymbol: Choice<Terminal, Noise>
    /// The symbol that represents the group's start.
    Start: GroupStart
    /// The symbol that represents the group's end.
    /// None (or null in C#) means that the group is ended by a new line.
    [<Nullable(2uy, 1uy)>] End: GroupEnd option
    /// The way the group advances.
    AdvanceMode: AdvanceMode
    /// The way the group ends.
    EndingMode: EndingMode
    /// A set of indexes whose corresponding groups can be nested inside this group.
    Nesting: ImmutableHashSet<uint32>
}
with
    /// Whether this group's content is a terminal.
    member x.IsTerminal =
        match x.ContainerSymbol with
        | Choice1Of2 _ -> true
        | Choice2Of2 _ -> false
    /// Whether this group is ended by a new line.
    member x.IsEndedByNewline = x.End.IsNone
    /// Whether this group is ended by the given `DFASymbol`.
    member x.IsEndedBy (sym: DFASymbol) =
        match sym, x.End with
        | Choice1Of4 (Terminal(_, name)), None
        | Choice2Of4 (Noise(name)), None ->
            // GOLD Parser might use a different capitalization for NewLines.
            name.Equals("NewLine", StringComparison.OrdinalIgnoreCase)
        | Choice4Of4 ge, Some ge' -> ge = ge'
        | _ -> false
    override x.ToString() = x.Name

/// A DFA state. It defines the logic that produces tokens out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
[<DebuggerDisplay("{Index}")>]
type DFAState = {
    /// The index of the state in the DFA state table.
    Index: uint32
    /// The edges of the state, that match a character to a
    /// next state, using a custom data structure. A character
    /// can be set to explicitly fail, prohibiting the use of
    /// `AnythingElse`.
    [<Nullable(1uy, 2uy)>] Edges: RangeMap<char, uint32 option>
    /// Whether this state accepts a symbol or not.
    [<Nullable(2uy, 1uy)>] AcceptSymbol: DFASymbol option
    /// The state to maybe go to (or fail) in
    /// case the character had no matching edge.
    [<Nullable(2uy)>] AnythingElse: uint32 option
}

[<RequireQualifiedAccess>]
/// A symbol that is relevant to the LALR parser.
type LALRSymbol =
    /// The symbol is a terminal.
    | Terminal of Terminal
    /// The symbol is a nonterminal.
    | Nonterminal of Nonterminal
    with
        override x.ToString() =
            match x with
            | Terminal term -> string term
            | Nonterminal nont -> string nont

[<CustomEquality; CustomComparison>]
/// A sequence of `Terminal`s and `Nonterminal`s that can produce a specific `Nonterminal`.
type Production = {
    /// The index of the production. Productions
    /// with the same index are considered equal.
    Index: uint32
    /// The `Nonterminal` the production is referring to.
    // Storing the map's key (the nonterminal) inside its value (this production)
    // is acceptable, because the production's head is an integral part of its definition.
    Head: Nonterminal
    /// The `Terminals`s and `Nonterminal`s, the production is made of.
    Handle: LALRSymbol ImmutableArray
}
with
    /// Pretty-prints the members of a production to a string.
    static member internal Format(head, handle) =
        handle
        |> Seq.map string
        |> String.concat " "
        |> sprintf "%O ::= %s" head
    member x.Equals x' = x.Index = x'.Index
    member x.CompareTo x' = compare x.Index x'.Index
    override x.Equals x' =
        obj.ReferenceEquals(x, x') ||
        match x' with
        | :? Production as x' -> x.Index = x'.Index
        | _ -> false
    override x.GetHashCode() = x.Index.GetHashCode()
    override x.ToString() = Production.Format(x.Head, x.Handle)
    interface IEquatable<Production> with
        member x.Equals x' = x.Index = x'.Index
    interface IComparable<Production> with
        member x.CompareTo x' = compare x.Index x'.Index
    interface IComparable with
        member x.CompareTo x' = x.CompareTo (x' :?> _)

/// An action to be taken by the LALR parser according to the given `Terminal`.
[<RequireQualifiedAccess>]
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`.
    | Shift of StateIndex: uint32
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production: Production
    /// When the parser encounters this action for a given symbol,
    /// the input text is accepted as correct and parsing ends.
    | Accept
    override x.ToString() =
        match x with
        | Shift x -> sprintf "Shift: %d" x
        // Shift has a colon, Reduction doesn't.
        // This way, both words have the same number of characters.
        | Reduce x -> sprintf "Reduce %O" x
        | Accept -> "Accept"

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
type LALRState = {
    /// The index of the state.
    Index: uint32
    /// The available next `LALRAction`s of the state,
    /// depending on the next `Terminal` encountered.
    Actions: ImmutableDictionary<Terminal, LALRAction>
    /// The available `LALRAction` to be taken if input ends.
    [<Nullable(2uy, 1uy)>] EOFAction: LALRAction option
    /// The available GOTO actions of the state. These actions
    /// are used when a production is reduced and the parser
    /// jumps to the state that represents the shifted nonterminal.
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

/// This interface is implemented by <see cref="Grammar"/>s and
/// <see cref="RuntimeFarkle{TResult}"/>s and provides a uniform
/// way to work with grammars inside runtime Farkles and grammars
/// themselves, while accounting for potential build errors in an
/// exception-free way.
type IGrammarProvider =
    /// <summary>Whether the grammar has been successfuly built.</summary>
    /// <remarks>A <see langword="true"/> value of this property indicates
    /// that <see cref="GetGrammar"/> is safe to be called.</remarks>
    abstract IsBuildSuccessful: bool
    /// <summary>Gets the <see cref="Grammar"/> of this provider.</summary>
    /// <exception cref="InvalidOperationException">This method was called
    /// with <see cref="IsBuildSuccessful"/> being false.</exception>
    abstract GetGrammar: unit -> Grammar
    /// Returns a user-friendly error message that
    /// describes what had gone wrong while building,
    /// or an empty string if building had been successful.
    abstract GetBuildErrorMessage: unit -> string

/// A context-free grammar according to which Farkle can parse text.
and [<NoComparison; ReferenceEquality>] Grammar = internal {
    // This field is totally informative; it serves only the template maker.
    _Properties: ImmutableDictionary<string,string>

    // These fields serve the template maker again, but the information
    // they carry is redundantly stored here for his convenience.
    _StartSymbol: Nonterminal
    _Symbols: Symbols
    _Productions: Production ImmutableArray

    // These are the only fields that serve the parser.
    _Groups: Group ImmutableArray
    _LALRStates: LALRState ImmutableArray
    _DFAStates: DFAState ImmutableArray
}
with
    /// Key-value pairs of strings that store informative properties about the grammar.
    /// See the [GOLD Parser's documentation](http://www.goldparser.org/doc/egt/index.htm).
    member x.Properties = x._Properties
    /// The grammar's start `Nonterminal`.
    member x.StartSymbol = x._StartSymbol
    /// The grammar's terminals, nonterminals, and noise symbols.
    member x.Symbols = x._Symbols
    /// The grammar's `Production`s.
    member x.Productions = x._Productions
    /// The grammar's `Group`s.
    member x.Groups = x._Groups
    /// The grammar's LALR state table.
    member x.LALRStates = x._LALRStates
    /// The grammar's DFA state table.
    member x.DFAStates = x._DFAStates
    /// <summary>Finds a <see cref="Terminal"/> with a specific name.</summary>
    /// <param name="name">The terminal's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">No
    /// terminal named <paramref name="name"/> exists.</exception>
    /// <exception cref="InvalidOperationException">Many
    /// terminals named <paramref name="name"/> exist.</exception>
    /// <remarks>This method is useful for those that write custom
    /// <see cref="Farkle.Parser.Tokenizer"/>. It executes in linear
    /// time. For this reason, it is advised to be called once per terminal name.</remarks>
    member x.GetTerminalByName name =
        x.Symbols.Terminals
        |> Seq.filter (fun (Terminal(_, termName)) -> name = termName)
        |> List.ofSeq
        |> function
        | [term] -> term
        | [] ->
            raise (ArgumentOutOfRangeException("name", name, "No terminal with such name was found."))
        | _ ->
            invalidOp (sprintf "Many terminals named %s exist" name)
    interface IGrammarProvider with
        member _.IsBuildSuccessful = true
        member x.GetGrammar() = x
        member x.GetBuildErrorMessage() = ""
