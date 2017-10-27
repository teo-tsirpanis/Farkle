// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EgtReader

open Chessie.ErrorHandling
open FSharpx.Collections
open Farkle
open Farkle.Grammar
open Farkle.Monads

module internal HighLevel =

    open StateResult
    open MidLevel

    type IndexedGetter<'a> = Indexed<'a> -> StateResult<'a, Entry list, EGTReadError>

    let wantUInt32 = wantUInt16 <!> uint32

    let liftFlatten x = x >>= liftResult

    let getIndexedfromList x = flip Indexed.getfromList x >> liftResult >> mapFailure IndexNotFound

    let readProperty = sresult {
        do! wantUInt16 |> ignore /// We do not store based on index
        let! name = wantString
        let! value = wantString
        return (name, value)
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
    }

    let readCharSet = sresult {
        let! index = wantUInt32
        do! wantUInt16 |> ignore // Unicode plane is ignored; what exactly does it do?
        do! wantUInt16 |> ignore // Range count is ignored; we just read until the end.
        do! wantEmpty // Reserved field.
        let readCharSet = sresult {
            let! start = wantUInt16 <!> char
            let! finish = wantUInt16 <!> char
            return RangeSet.create start finish
        }
        return!
            whileFull readCharSet
            <!> RangeSet.concat
            <!> (fun x -> x, index)
    }

    let readSymbol = sresult {
        let! index = wantUInt32
        let! name = wantString
        let! result =
            wantUInt16
            <!> Symbol.create name
            |> liftFlatten
        return IndexableWrapper.create index result
    }

    let readGroup fSymbols = sresult {
        let! index = wantUInt32
        let! name = wantString
        let symbolFunc = wantUInt32 <!> Indexed >>= fSymbols
        let! contIndex = symbolFunc
        let! startIndex = symbolFunc
        let! endIndex = symbolFunc
        let! advMode = wantUInt16 <!> AdvanceMode.create |> liftFlatten
        let! endingMode = wantUInt16 <!> EndingMode.create |> liftFlatten
        do! wantEmpty // Reserved field.
        do! wantUInt16 |> ignore // Nesting count is ignored; we just read until the end.
        let nestingFunc = wantUInt32 <!> Indexed
        let! nesting = whileFull nestingFunc <!> set
        return
            {
                Name = name
                Index = index
                ContainerSymbol = contIndex
                StartSymbol = startIndex
                EndSymbol = endIndex
                AdvanceMode = advMode
                EndingMode = endingMode
                Nesting = nesting
            }
    }

    let readProduction (fSymbols: IndexedGetter<_>) = sresult {
        let! index = wantUInt32
        let symbolFunc = wantUInt32 <!> Indexed >>= fSymbols
        let! head = symbolFunc
        do! wantEmpty // Reserved field.
        let! handle = whileFull symbolFunc <!> List.ofSeq
        return
            {
                Index = index
                Head = head
                Handle = handle
            }
    }

    let readInitialStates = sresult {
        let! dfa = wantUInt32 <!> Indexed
        let! lalr = wantUInt32 <!> Indexed
        return dfa, lalr
    }

    let readDFAState (fSymbols: IndexedGetter<_>) (fCharSets: IndexedGetter<_>) = sresult {
        let! index = wantUInt32
        let! isAccept = wantBoolean
        let! acceptIndex = wantUInt32 <!> Indexed
        do! wantEmpty // Reserved field.
        let readEdges = sresult {
            let! charSet = wantUInt32 <!> Indexed >>= fCharSets
            let! target = wantUInt32 <!> Indexed
            do! wantEmpty // Reserved field.
            return charSet, target
        }
        let! edges = whileFull readEdges <!> List.ofSeq
        match isAccept with
           | true -> return! acceptIndex |> fSymbols <!> (fun x -> DFAAccept (index, (x, edges)))
           | false -> return DFAContinue (index, edges)
    }

    let readLALRState (fSymbols: IndexedGetter<_>) fProds = sresult {
        let! index = wantUInt32
        do! wantEmpty // Reserved field.
        let readActions = sresult {
            let! symbolIndex = wantUInt32 <!> Indexed >>= fSymbols
            let! actionId = wantUInt16
            let! targetIndex = wantUInt32
            do! wantEmpty // Reserved field.
            let! action = LALRAction.create fProds targetIndex actionId |> liftResult
            return (symbolIndex, action)
        }
        let! states = whileFull readActions <!> Map.ofSeq
        return {States = states; Index = index}
    }

    let mapMatching magicChar f = sresult {
        let! x = wantByte
        if x = magicChar then
            return! f <!> Some
        else
            return None
    }

    let makeGrammar (EGTFile records) = trial {
        let mapMatching mc f = records |> Seq.map (fun (Record x) -> eval (mapMatching mc f) x) |> collect |> lift (Seq.choose id)
        let! properties = readProperty |> mapMatching 'p'B |> lift (Map.ofSeq >> Properties)
        let! tableCounts = readTableCounts |> mapMatching 't'B |> lift Seq.head
        let! charSets = readCharSet |> mapMatching 'c'B |> lift (Seq.sortBy snd >> Seq.map fst >> RandomAccessList.ofSeq)
        let fCharSets = getIndexedfromList charSets
        let! symbols = readSymbol |> mapMatching 'S'B |> lift IndexableWrapper.collect
        let fSymbols = getIndexedfromList symbols
        let! groups = readGroup fSymbols |> mapMatching 'g'B |> lift Indexable.collect
        let! prods = readProduction fSymbols |> mapMatching 'R'B |> lift Indexable.collect
        let fProds x = eval (getIndexedfromList prods x) ()
        let! (initialDFA, initialLALR) = readInitialStates |> mapMatching 'I'B |> lift Seq.head
        let! dfas = readDFAState fSymbols fCharSets |> mapMatching 'D'B |> Trial.bind (Indexable.collect >> StateTable.create initialDFA >> Trial.mapFailure IndexNotFound)
        let! lalrs = readLALRState fSymbols fProds |> mapMatching 'L'B |> Trial.bind (Indexable.collect >> StateTable.create initialLALR >> Trial.mapFailure IndexNotFound)
        return! Grammar.create properties symbols charSets prods dfas lalrs groups tableCounts
    }
    // From 20/7/2017 until 24/7/2017, this file had _exactly_ 198 lines of code.
    // This Number should not be changed, unless it was absolutely neccessary.
    // Was that a coincidence? Highly unlikely.
    // Just look! Not even the dates were a coincidence! 🔺👁
