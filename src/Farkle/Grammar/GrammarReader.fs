// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.Monads.Maybe

module internal GrammarReader =

    module private Implementation =

        // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
        // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
        // FFFFFFfFFFFFFF
        let inline wantUInt16 x = match x with | UInt16 x -> Some x | _ -> None

        let inline readToEnd allowEmpty count fReadIt =
            function
            | [] when allowEmpty -> Some []
            | x -> x |> List.chunkBySize count |> List.map fReadIt |> List.allSome

        let readProperty =
            function
            | [UInt16 _index; String name; String value] -> (name, value) |> Some
            | _ -> None

        let readTableCounts =
            function
            | [UInt16 symbols; UInt16 sets; UInt16 rules; UInt16 dfas; UInt16 lalrs; UInt16 groups] ->
                Some
                    {
                        SymbolTables = symbols
                        CharSetTables = sets
                        ProductionTables = rules
                        DFATables = dfas
                        LALRTables = lalrs
                        GroupTables = groups
                    }
            | _ -> None

        let readCharSet _ =
            let readRanges =
                function
                | [UInt16 start; UInt16 theEnd] ->
                    RangeSet.create (char start) (char theEnd)
                    |> Some
                | _ -> None
                |> readToEnd false 2
                >> Option.map RangeSet.concat
            function
            | UInt16 _unicodePlane :: UInt16 _rangeCount
                :: Empty :: ranges ->
                ranges
                |> readRanges
            | _ -> None

        let readSymbol index =
            function
            | [String name; UInt16 0us] -> Nonterminal (uint32 index, name) |> Some
            | [String name; UInt16 1us] -> Terminal (uint32 index, name) |> Some
            | [String name; UInt16 2us] -> Noise name |> Some
            | [String _ ; UInt16 3us] -> EndOfFile |> Some
            | [String name; UInt16 4us] -> GroupStart name |> Some
            | [String name; UInt16 5us] -> GroupEnd name |> Some
            | [String _; UInt16 7us] -> Error |> Some
            | _ -> None

        let readGroup fSymbol fGroup index =
            let readNestedGroups =
                (List.exactlyOne >> wantUInt16 >> Option.bind fGroup)
                |> readToEnd true 1
                >> Option.map set
            function
            | String name :: UInt16 containerIndex :: UInt16 startIndex :: UInt16 endIndex :: UInt16 advanceMode :: UInt16 endingMode :: Empty :: UInt16 _nestingCount :: xs -> maybe {
                let! containerSymbol = fSymbol containerIndex
                let! startSymbol = fSymbol startIndex
                let! endSymbol = fSymbol endIndex
                let! advanceMode = match advanceMode with | 0us -> Some Token | 1us -> Some Character | _ -> None
                let! endingMode = match endingMode with | 0us -> Some Open | 1us -> Some Closed | _ -> None
                let! nesting = readNestedGroups xs
                return {
                    Name = name
                    Index = index
                    ContainerSymbol = containerSymbol
                    StartSymbol = startSymbol
                    EndSymbol = endSymbol
                    AdvanceMode = advanceMode
                    EndingMode = endingMode
                    Nesting = nesting
                }
                }
            | _ -> None

        let readProduction fSymbol index =
            let readChildrenSymbols =
                (List.exactlyOne >> wantUInt16 >> Option.bind fSymbol)
                |> readToEnd true 1
            function
            | UInt16 headIndex :: Empty :: xs -> maybe {
                let! headSymbol = fSymbol headIndex
                let! symbols = readChildrenSymbols xs
                return {Index = index; Head = headSymbol; Handle = symbols}
                }
            | _ -> None

        let readInitialStates fDFA fLALR =
            function
            | [UInt16 dfa; UInt16 lalr] -> maybe {
                let! dfa = fDFA dfa
                let! lalr = fLALR lalr
                return dfa, lalr
                }
            | _ -> None

        let readDFAState fCharSet fSymbol fDFA index =
            let readDFAEdges =
                function
                | [UInt16 charSetIndex; UInt16 targetIndex; Empty] -> maybe {
                    let! charSet = fCharSet charSetIndex
                    let! target = fDFA targetIndex
                    return (charSet, target)
                    }
                | _ -> None
                |> readToEnd false 3
            function
            | Boolean false :: UInt16 _ :: Empty :: xs ->
                xs
                |> readDFAEdges
                |> Option.map (fun edges -> (index, edges) |> DFAContinue)
            | Boolean true :: UInt16 acceptIndex :: Empty :: xs -> maybe {
                let! edges = readDFAEdges xs
                let! acceptSymbol = fSymbol acceptIndex
                return DFAAccept (index, (acceptSymbol, edges))
                }
            | _ -> None

        let readLALRState fSymbol fProduction fLALR index =
            let readLALRAction =
                function
                | UInt16 symbolIndex :: xs -> maybe {
                    let! symbol = fSymbol symbolIndex
                    match xs with
                    | [UInt16 1us; UInt16 targetStateIndex; Empty] ->
                        let! targetState = fLALR targetStateIndex
                        return symbol, Shift targetState
                    | [UInt16 2us; UInt16 targetProductionIndex; Empty] ->
                        let! targetProduction = fProduction targetProductionIndex
                        return symbol, Reduce targetProduction
                    | [UInt16 3us; UInt16 targetStateIndex; Empty] ->
                        let! targetState = fLALR targetStateIndex
                        return symbol, Goto targetState
                    | [UInt16 4us; UInt16 _; Empty] -> return symbol, Accept
                    | _ -> return! None
                    }
                | _ -> None
                |> readToEnd false 4
                >> Option.map Map.ofSeq
            function
            | Empty :: xs -> readLALRAction xs |> Option.map (fun actions -> {Index = index; Actions = actions})
            | _ -> None

        let getSingleElement magicCode f =
            Map.tryFind magicCode
            >> Option.bind (function | [x] -> Some x | _ -> None)
            >> Option.bind f

        let getIndexedElements magicCode f length =
            let impl opt entries =
                match opt, entries with
                | Some array, UInt16 index :: xs -> maybe {
                    let! x = f (uint32 index) xs
                    let index = int index
                    if index < Array.length array then
                        do Array.set array index x
                        return array
                    else
                        return! None
                    }
                | _ -> None
            let array = length * 1us |> int |> Array.zeroCreate
            Map.tryFind magicCode
            >> Option.defaultValue []
            >> List.fold (impl) (Some array)
            >> Option.map SafeArray.ofSeq

        let collectRecords =
            List.map (function | Byte magicCode :: xs -> Some (magicCode, xs) | _ -> None)
            >> List.allSome
            >> Option.map (List.groupBy fst >> List.map (fun (mc, x) -> mc , List.map snd x) >> Map.ofList)

        [<Literal>]
        let CGTHeader = "GOLD Parser Tables/v1.0"
        [<Literal>]
        let EGTHeader = "GOLD Parser Tables/v5.0"

        let makeGrammar {Header = header; Records = records} =
            match header with
            | CGTHeader -> Result.Error ReadACGTFile
            | EGTHeader -> failIfNone UnknownEGTFile <| maybe {
                let! records = records |> collectRecords

                let! properties =
                    records
                    |> Map.tryFind 'p'B
                    |> Option.defaultValue [] // We don't care if there are no properties.
                    |> List.map readProperty
                    |> List.allSome
                    |> Option.map (Map.ofList >> Properties)

                let! tableCounts = getSingleElement 't'B readTableCounts records

                let! charSets = getIndexedElements 'c'B readCharSet tableCounts.CharSetTables records
                let fCharSet = uint32 >> SafeArray.getUnsafe charSets

                let! symbols = getIndexedElements 'S'B readSymbol tableCounts.SymbolTables records
                let fSymbol = uint32 >> SafeArray.getUnsafe symbols

                let fGroup = Indexed.createWithKnownLength16 tableCounts.GroupTables
                let! groups = getIndexedElements 'g'B (readGroup fSymbol fGroup) tableCounts.GroupTables records

                let! productions = getIndexedElements 'R'B (readProduction fSymbol) tableCounts.ProductionTables records
                let fProduction = uint32 >> SafeArray.getUnsafe productions

                let fDFA = Indexed.createWithKnownLength16 tableCounts.DFATables
                let fLALR = Indexed.createWithKnownLength16 tableCounts.LALRTables
                let! initialDFA, initialLALR = getSingleElement 'I'B (readInitialStates fDFA fLALR) records

                let! dfaStates = getIndexedElements 'D'B (readDFAState fCharSet fSymbol fDFA) tableCounts.DFATables records
                let dfaStateTable = {InitialState = dfaStates.Item initialDFA; States = dfaStates}

                let! lalrStates = getIndexedElements 'L'B (readLALRState fSymbol fProduction fLALR) tableCounts.LALRTables records
                let lalrStateTable = {InitialState = lalrStates.Item initialLALR; States = lalrStates}

                return GOLDGrammar.create properties symbols charSets productions dfaStateTable lalrStateTable groups
                }
            | _ -> Result.Error UnknownEGTFile
        
        let inline zc x = x |> int |> Array.zeroCreate

        let inline itemTry arr idx = idx |> int |> flip Array.tryItem arr

        let readAndAssignIndexed fRead arr entries =
            match entries with
            | UInt16 index :: xs -> maybe {
                let! x = fRead (uint32 index) xs
                let index = int index
                if index < Array.length arr then
                    do Array.set arr index x
                else
                    return! None
                }
            | _ -> None

        let inline changeOnce x newValue =
            match !x with
            | None ->
                x := Some newValue
                Some ()
            | Some _ -> None

        let makeGrammar2 r =
            let properties = System.Collections.Generic.Dictionary()
            let mutable isTableCountsInitialized = false
            let mutable charSets = [| |]
            let mutable symbols = [| |]
            let mutable groups = [| |]
            let mutable productions = [| |]
            let mutable dfaStates = [| |]
            let mutable lalrStates = [| |]
            let initialStates = ref None
            let fHeaderCheck =
                function
                | CGTHeader -> fail ReadACGTFile
                | EGTHeader -> Ok ()
                | _ -> fail UnknownEGTFile
            let initTables (x: TableCounts) =
                charSets <- zc x.CharSetTables
                symbols <- zc x.SymbolTables
                groups <- zc x.GroupTables
                productions <- zc x.ProductionTables
                dfaStates <- zc x.DFATables
                lalrStates <- zc x.LALRTables
            let fRecord =
                function
                | Byte 'p'B :: xs -> readProperty xs |> Option.map (properties.Add)
                // The table counts record must exist only once, and before the other records.
                | Byte 't'B :: xs when not isTableCountsInitialized ->
                    isTableCountsInitialized <- true
                    readTableCounts xs |> Option.map initTables
                | Byte 'c'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed readCharSet charSets xs
                | Byte 'S'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed readSymbol symbols xs
                | Byte 'g'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed (readGroup (itemTry symbols) (Indexed.createWithKnownLength groups)) groups xs
                | Byte 'R'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed (readProduction (itemTry symbols)) productions xs
                | Byte 'I'B :: xs when isTableCountsInitialized ->
                    readInitialStates (Indexed.createWithKnownLength dfaStates) (Indexed.createWithKnownLength lalrStates) xs |> Option.bind (changeOnce initialStates)
                | Byte 'D'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed (readDFAState (itemTry charSets) (itemTry symbols) (Indexed.createWithKnownLength dfaStates)) dfaStates xs
                | Byte 'L'B :: xs when isTableCountsInitialized ->
                    readAndAssignIndexed (readLALRState (itemTry symbols) (itemTry productions) (Indexed.createWithKnownLength lalrStates)) lalrStates xs
                | _ -> None
                >> failIfNone UnknownEGTFile
            either {
                do! EGTReader.readEGT2 fHeaderCheck fRecord r
                let! (initialDFA, initialLALR) = !initialStates |> failIfNone UnknownEGTFile
                let dfaStates = SafeArray.ofSeq dfaStates
                let lalrStates = SafeArray.ofSeq lalrStates
                return GOLDGrammar.create
                    (properties |> Seq.map (fun p -> p.Key, p.Value) |> Map.ofSeq |> Properties)
                    (SafeArray.ofSeq symbols)
                    (SafeArray.ofSeq charSets)
                    (SafeArray.ofSeq productions)
                    {InitialState = dfaStates.Item initialDFA; States = dfaStates}
                    {InitialState = lalrStates.Item initialLALR; States = lalrStates}
                    (SafeArray.ofSeq groups)
            }

    let read = Implementation.makeGrammar

    let read2 = Implementation.makeGrammar2
