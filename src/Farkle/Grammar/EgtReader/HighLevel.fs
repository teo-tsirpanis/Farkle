// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EgtReader

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads

module internal HighLevel =

    open StateResult
    open MidLevel

    type Indexable<'a> = Indexable<'a, uint16>

    type RecordType =
        | DFAState of Indexable<DFAState>
        | InitialStates of InitialStates
        | LALRState of Indexable<LALRState>
        | Production of Indexable<Production>
        | Symbol of Indexable<Symbol>
        | Charset of Indexable<CharSet>
        | Group of Indexable<Group>
        | Property of string * string
        | TableCounts of TableCounts

    let liftFlatten x = x <!> liftResult |> flatten

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
            whileM (List.isEmpty() |> liftState) readCharSet
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
        let! nesting = whileM (List.isEmpty() |> liftState) nestingFunc <!> set
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
        let! symbols = whileM (List.isEmpty() |> liftState) symbolFunc <!> List.ofSeq
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
        let! edges = whileM (List.isEmpty() |> liftState) readEdges <!> set
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
            whileM (List.isEmpty() |> liftState) readActions <!> Map.ofSeq
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
        |> List.map (fun (Record x) -> eval nailIt x)
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
        let exactlyOne x = x |> List.exactlyOne |> Trial.mapFailure (List.map ListError)
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
