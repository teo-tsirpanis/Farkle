// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar.EgtReader
open Farkle.Monads

module GrammarReader =

    open StateResult

    type Indexable<'a> = Indexable<'a, uint16>

    type RecordType =
        | DFAState of Indexable<DFAState> //= 68uy
        | InitialStates of InitialStates //= 73uy
        | LALRState of Indexable<LALRState> //= 76uy
        | Production of Indexable<Production> //= 82uy
        | Symbol of Indexable<Symbol> //= 83uy
        | Charset of Indexable<CharSet> //= 99uy
        | Group of Indexable<Group> //= 103uy
        | Property of string * string //= 112uy
        | TableCounts of TableCounts //= 116uy

    let liftFlatten x = x <!> liftResult |> flatten

    let eitherEntry fEmpty fByte fBoolean fUInt16 fString = sresult {
        let! entry = Seq.takeOne() |> mapFailure (SeqError >> EGTReadError)
        return!
            match entry with
            | Empty -> fEmpty entry ()
            | Byte x -> fByte entry x
            | Boolean x -> fBoolean entry x
            | UInt16 x -> fUInt16 entry x
            | String x -> fString entry x
    }

    let wantEmpty, wantByte, wantBoolean, wantUInt16, wantString =
        let fail x = fun entry _ -> (x, entry) |> InvalidEntryType |> EGTReadError |> StateResult.fail
        let failEmpty x = fail "Empty" x
        let failByte x = fail "Byte" x
        let failBoolean x = fail "Boolean" x
        let failUInt16 x = fail "UInt16" x
        let failString x = fail "String" x
        let ok _ x = returnM x
        let wantEmpty  = eitherEntry ok failByte failBoolean failUInt16 failString
        let wantByte = eitherEntry failEmpty ok failBoolean failUInt16 failString
        let wantBoolean = eitherEntry failEmpty failByte ok failUInt16 failString
        let wantUInt16 = eitherEntry failEmpty failByte failBoolean ok failString
        let wantString = eitherEntry failEmpty failByte failBoolean failUInt16 ok
        wantEmpty, wantByte, wantBoolean, wantUInt16, wantString

    let readProperty = sresult {
        do! wantUInt16 |> ignore /// We do not store based on index
        let! name = wantString
        let! value = wantString
        return (name, value) |> Property
    }

    let readTableCounts = sresult {
        let! symbols = wantUInt16
        let! charSets = wantUInt16
        let! productions = wantUInt16
        let! dfas = wantUInt16
        let! lalrs = wantUInt16
        let! groups = wantUInt16
        return
            {
                SymbolTables = symbols
                CharSetTables = charSets
                ProductionTables = productions
                DFATables = dfas
                LALRTables = lalrs
                GroupTables = groups
            }
            |> TableCounts
    }

    let readCharSet = sresult {
        let! index = wantUInt16
        do! wantUInt16 |> ignore // Unicode plane is ignored; what exactly does it do?
        do! wantUInt16 |> ignore // Range count is ignored; we just read until the end.
        do! wantEmpty // Reserved field.
        let readCharSet = sresult {
            let! start = wantUInt16 <!> char
            let! finish = wantUInt16 <!> char
            return RangeSet.create start finish
        }
        return!
            whileM (Seq.isEmpty() |> liftState) readCharSet
            <!> RangeSet.concat
            <!> Indexable.create index
            <!> Charset
    }

    let readSymbol = sresult {
        let! index = wantUInt16
        let! name = wantString
        let! stype =
            wantUInt16
            <!> SymbolType.ofUInt16
            |> liftFlatten
        return
            {
                Name = name
                Kind = stype
            }
            |> Indexable.create index
            |> Symbol
    }

    let readGroup = sresult {
        let! index = wantUInt16
        let! name = wantString
        let! contIndex = wantUInt16 <!> Indexed
        let! startIndex = wantUInt16 <!> Indexed
        let! endIndex = wantUInt16 <!> Indexed
        let! advMode = wantUInt16 <!> AdvanceMode.create |> liftFlatten
        let! endingMode = wantUInt16 <!> EndingMode.create |> liftFlatten
        do! wantEmpty // Reserved field.
        do! wantUInt16 |> ignore // Nesting count is ignored; we just read until the end.
        let nestingFunc = wantUInt16 <!> Indexed
        let! nesting = whileM (Seq.isEmpty() |> liftState) nestingFunc <!> set
        return
            {
                Name = name
                ContainerSymbol = contIndex
                StartSymbol = startIndex
                EndSymbol = endIndex
                AdvanceMode = advMode
                EndingMode = endingMode
                Nesting = nesting
            }
            |> Indexable.create index
            |> Group
    }

    let readProduction = sresult {
        let! index = wantUInt16
        let! nonTerminal = wantUInt16 <!> Indexed
        do! wantEmpty // Reserved field.
        let symbolFunc = wantUInt16 <!> Indexed
        let! symbols = whileM (Seq.isEmpty() |> liftState) symbolFunc <!> List.ofSeq
        return
            {
                Nonterminal = nonTerminal
                Symbols = symbols
            }
            |> Indexable.create index
            |> Production
    }

    let readInitialStates = sresult {
        let! dfa = wantUInt16
        let! lalr = wantUInt16
        return
            {
                DFA = dfa
                LALR = lalr
            }
            |> InitialStates
    }

    let readDFAState = sresult {
        let! index = wantUInt16
        let! isAccept = wantBoolean
        let! acceptIndex = wantUInt16 <!> Indexed
        let acceptState =
            match isAccept with
            | true -> Some acceptIndex
            | false -> None
        do! wantEmpty // Reserved field.
        let readEdges = sresult {
            let! charSet = wantUInt16 <!> Indexed
            let! target = wantUInt16 <!> Indexed
            do! wantEmpty // Reserved field.
            return charSet, target
        }
        let! edges = whileM (Seq.isEmpty() |> liftState) readEdges <!> set
        return
            {
                AcceptSymbol = acceptState
                Edges = edges
            }
            |> Indexable.create index
            |> DFAState
    }

    let readLALRState = sresult {
        let! index = wantUInt16
        do! wantEmpty // Reserved field.
        let readActions = sresult {
            let! symbolIndex = wantUInt16 <!> Indexed
            let! actionId = wantUInt16
            let! targetIndex = wantUInt16
            do! wantEmpty // Reserved field.
            let! action = LALRAction.create targetIndex actionId |> liftResult
            return (symbolIndex, action)
        }
        return!
            whileM (Seq.isEmpty() |> liftState) readActions <!> Map.ofSeq
            <!> LALRState.LALRState
            <!> Indexable.create index
            <!> LALRState
    }

    let recordLookup =
        [
            'P', readProperty
            't', readTableCounts
            'c', readCharSet
            'S', readSymbol
            'g', readGroup
            'R', readProduction
            'I', readInitialStates
            'D', readDFAState
            'L', readLALRState
        ]
        |> Map.ofList


    let readEGTRecords (EGTFile x) =
        let nailIt = sresult {
            let! x = wantByte <!> char
            return! recordLookup.[x]
        }
        x
        |> List.map (fun (Record x) -> x |> Seq.ofList |> eval nailIt)
        |> collect

    let eitherRecord fProperty fTableCounts fCharSet fSymbol fGroup fProduction fInitialStates fDFA fLALR =
        function
        | Property (x, y) -> fProperty (x, y)
        | TableCounts x -> fTableCounts x
        | Charset x -> fCharSet x
        | Symbol x -> fSymbol x
        | Group x -> fGroup x
        | Production x -> fProduction x
        | InitialStates x -> fInitialStates x
        | DFAState x -> fDFA x
        | LALRState x -> fLALR x

    let makeGrammar records = trial {
        let none _ = None
        let choose f = records |> List.choose f
        let exactlyOne x = x |> Seq.exactlyOne |> Trial.mapFailure (List.map (SeqError >> EGTReadError))
        let properties = choose <| eitherRecord Some none none none none none none none none |> Map.ofList |> Properties
        let! tableCounts = choose <| eitherRecord none Some none none none none none none none |> exactlyOne
        let charSets = choose <| eitherRecord none none Some none none none none none none |> Indexable.collect
        let symbols = choose <| eitherRecord none none none Some none none none none none |> Indexable.collect
        let groups = choose <| eitherRecord none none none none Some none none none none |> Indexable.collect
        let prods = choose <| eitherRecord none none none none none Some none none none |> Indexable.collect
        let! initialStates = choose <| eitherRecord none none none none none none Some none none |> exactlyOne
        let dfas = choose <| eitherRecord none none none none none none none Some none |> Indexable.collect
        let lalrs = choose <| eitherRecord none none none none none none none none Some |> Indexable.collect
        return! Grammar.create properties symbols charSets prods initialStates dfas lalrs groups tableCounts
    }
