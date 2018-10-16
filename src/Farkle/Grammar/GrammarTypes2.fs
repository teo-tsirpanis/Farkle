// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar2

open Farkle.Collections
open System.Collections.Immutable

/// A type indicating how a group advances.
[<Struct>]
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

/// A type indicating how the ending symbol of a group is handled.
[<Struct>]
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

/// A symbol which is produced through a DFA, and is significant for the grammar.
type Terminal = Terminal of index: uint32 * name: string

/// A symbol which is produced through a DFA, but is not significant for the grammar and is discarded.
/// An example of a noise symbol would be a source code comment.
type Noise = Noise of name: string

/// A symbol signifying the end of a group.
type GroupEnd = GroupEnd of name: string

/// A symbol signifying the start of a group.
type GroupStart = GroupStart of name: string * groupIndex: Indexed<Group>

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

/// A symbol which is produced by a concatenation of other `LALRSymbol`s, as the LALR parser dictates.
type NonTerminal = internal NonTerminal of name: string

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

/// A symbol that is part of `NonTerminal`s, and can be produced by the LALR parser.
and LALRSymbol = Choice<Terminal, NonTerminal>

/// An action to be taken by the LALR parser.
[<RequireQualifiedAccess>]
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`.
    | Shift of Indexed<LALRState>
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// This action is used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    | Goto of Indexed<LALRState>
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and parsing ends.
    | Accept

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
and LALRState = {
    /// The index of the state.
    Index: uint32
    /// The available `LALRAction`s of the state.
    /// Depending on the symbol, the next action to be taken is determined.
    /// In case of an end-of-input, the corresponding action - if it exists - will have a key of `None`.
    Actions: Map<LALRSymbol option, LALRAction>
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
    /// Mmetadata about the grammar. See the [GOLD Parser's documentation for more](http://www.goldparser.org/doc/egt/index.htm).
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
