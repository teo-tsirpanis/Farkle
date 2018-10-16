// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar2

open Farkle.Collections
open System.Collections.Immutable
open System

/// A type indicating how a group advances.
[<Struct; RequireQualifiedAccess>]
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

/// A type indicating how the ending symbol of a group is handled.
[<Struct; RequireQualifiedAccess>]
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

/// A symbol which is produced through a DFA, and is significant for the grammar.
type Terminal = internal Terminal of index: uint32 * name: string
with
    /// The terminal's index.
    /// It is mainly used for the post-processor.
    member x.Index = match x with | Terminal (idx, _) -> idx
    /// The symbol's name.
    member x.Name = match x with | Terminal (_, name) -> name
    override x.ToString() =
        let name = x.Name
        if
            String.IsNullOrEmpty name
            || Char.IsLetter name.[0]
            || String.forall (fun x -> Char.IsLetter x || x = '.' || x = '-' || x = '_') name
        then
            "'" + name + "'"
        else
            name

/// A symbol which is produced by a concatenation of other `LALRSymbol`s, as the LALR parser dictates.
type NonTerminal = internal NonTerminal of name: string
with
    /// The nonterminal's name.
    member x.Name = match x with | NonTerminal (name) -> name
    override x.ToString() = "<" + x.Name + ">"

/// A symbol which is produced through a DFA, but is not significant for the grammar and is discarded.
/// An example of a noise symbol would be a source code comment.
type Noise = internal Noise of name: string
with
    /// The symbol's name.
    member x.Name = match x with | Noise (name) -> name
    override x.ToString() = "(" + x.Name + ")"

/// A symbol signifying the end of a group.
type GroupEnd = internal GroupEnd of name: string
with
    /// The symbol's name.
    member x.Name = match x with | GroupEnd (name) -> name
    override x.ToString() = "(" + x.Name + ")"

/// A symbol signifying the start of a group.
type GroupStart = internal GroupStart of name: string * groupIndex: Indexed<Group>
with
    /// The symbol's name.
    member x.Name = match x with | GroupStart (name, _) -> name
    override x.ToString() = "(" + x.Name + ")"

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
    End: GroupEnd
    /// The way the group advances.
    AdvanceMode: AdvanceMode
    /// The way the group ends.
    EndingMode: EndingMode
    /// A set of indexes whose corresponding groups can be nested inside this group.
    Nesting: Set<Indexed<Group>>
}

/// A symbol that can be yielded by the DFA.
type TokenSymbol = Choice<Terminal, Noise, GroupStart, GroupEnd>

/// An edge of a DFA graph, that matches a character to the next DFA state using a custom data structure.
type DFAEdge = RangeMap<char, Indexed<DFAState>>

/// A DFA state. It defines the logic that produces tokens out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
and [<RequireQualifiedAccess>] DFAState =
    /// This state does not accept a symbol. If the state graph cannot be further walked and
    /// an accepting state has not been found, tokenizing fails.
    | Continue of index: uint32 * DFAEdge
    /// This state accepts a symbol. If the state graph cannot be further walked, the included `Symbol` is returned.
    | Accept of index: uint32 * Choice<Terminal,Noise> * DFAEdge

/// A symbol that is part of `NonTerminal`s, and can be produced by the LALR parser.
type LALRSymbol = Choice<Terminal, NonTerminal>

/// An array of `LALRSymbol`s that can produce a specific `NonTerminal`.
type Production = {
    /// The index of the production.
    Index: uint32
    /// The `Nonterminal` the production is referring to.
    // Storing the map's key (the nonterminal) inside its value (this production)
    // is acceptable, because the production's head is an integral part of its definition.
    HeadSymbol: NonTerminal
    /// The `LALRSymbol`s the production is made of.
    Children: LALRSymbol ImmutableArray
}

/// An action to be taken by the LALR parser according to the given `Terminal`.
[<RequireQualifiedAccess>]
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`.
    | Shift of Indexed<LALRState>
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and parsing ends.
    | Accept

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
and LALRState = {
    /// The index of the state.
    Index: uint32
    /// The available next `LALRAction`s of the state.
    /// In case of an end-of-input, the corresponding action - if it exists - will have a key of `None`.
    Actions: Map<Terminal option, LALRAction>
    /// The available GOTO actions of the state.
    /// These actions are used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    GotoActions: Map<NonTerminal, Indexed<LALRState>>
}

/// A context-free grammar according to which, Farkle can parse text.
type Grammar = internal {
    _Properties: Map<string,string>

    _StartSymbol: NonTerminal
    _NonTerminalInfoMap: Map<NonTerminal, Production ImmutableArray>

    _Groups: Group SafeArray
    _LALRStates: LALRState StateTable
    _DFAStates: DFAState StateTable
}
with
    /// Metadata about the grammar. See the [GOLD Parser's documentation for more](http://www.goldparser.org/doc/egt/index.htm).
    member x.Properties = x._Properties
    /// The grammar's start `NonTerminal`.
    member x.StartSymbol = x._StartSymbol
    /// Gets the possible productions that can derive a `NonTerminal`.
    /// If it is not found, the array is empty.
    member x.GetNonTerminalInfo nt = x._NonTerminalInfoMap.TryFind nt |> Option.defaultValue ImmutableArray.Empty
    /// The grammar's `Group`s.
    member x.Groups = x._Groups
    /// The grammar's LALR state table.
    member x.LALRStates = x._LALRStates
    /// The grammar's DFA state table.
    member x.DFAStates = x._DFAStates
