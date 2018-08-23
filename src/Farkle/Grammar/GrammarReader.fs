// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.Monads.Maybe

module internal GrammarReader =

    module private Implementation =

        let consMaybe x xs = Option.map (List.cons x) xs

        let readToEnd allowEmpty fReadIt x =
            let rec impl x =
                match fReadIt x with
                | None -> None
                | Some (x, []) -> Some [x]
                | Some (x, xs) -> consMaybe x (impl xs)
            match x with
            | [] when allowEmpty -> Some []
            | x -> impl x

        let readProperty =
            function
            | [String name; String value] -> (name, value) |> Some
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
                | UInt16 start :: UInt16 theEnd :: xs ->
                    (RangeSet.create (char start) (char theEnd), xs)
                    |> Some
                | _ -> None
                |> readToEnd false
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
                (function | UInt16 x :: xs -> x |> fGroup |> Option.map (fun x -> x, xs) | _ -> None)
                |> readToEnd true
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
                (function | UInt16 x :: xs -> x |> fSymbol |> Option.map (fun x -> x, xs) | _ -> None)
                |> readToEnd true
            function
            | UInt16 headIndex :: Empty :: xs -> maybe {
                let! headSymbol = fSymbol headIndex
                let! symbols = readChildrenSymbols xs
                return {Index = index; Head = headSymbol; Handle = symbols}
                }
            | _ -> None

        let readInitialStates =
            function
            | [UInt16 dfa; UInt16 lalr] -> (dfa, lalr) |> Some
            | _ -> None

        let readDFAState fCharSet fSymbol fDFA index =
            let readDFAEdges =
                function
                | UInt16 charSetIndex :: UInt16 targetIndex :: Empty :: xs -> maybe {
                    let! charSet = fCharSet charSetIndex
                    let! target = fDFA targetIndex
                    return (charSet, target), xs
                    }
                | _ -> None
                |> readToEnd false
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
                    | UInt16 1us :: UInt16 targetStateIndex :: Empty :: xs ->
                        let! targetState = fLALR targetStateIndex
                        return (symbol, Shift targetState), xs
                    | UInt16 2us :: UInt16 targetProductionIndex :: Empty :: xs ->
                        let! targetProduction = fProduction targetProductionIndex
                        return (symbol, Reduce targetProduction), xs
                    | UInt16 3us :: UInt16 targetStateIndex :: Empty :: xs ->
                        let! targetState = fLALR targetStateIndex
                        return (symbol, Goto targetState), xs
                    | UInt16 4us :: UInt16 _ :: Empty :: xs -> return (symbol, Accept), xs
                    | _ -> return! None
                    }
                | _ -> None
                |> readToEnd false
                >> Option.map Map.ofSeq
            function
            | Empty :: xs -> readLALRAction xs |> Option.map (fun actions -> {Index = index; Actions = actions})
            | _ -> None

        let getSingleElement magicCode f =
            Map.tryFind magicCode
            >> Option.bind (function | [x] -> Some x | _ -> None)
            >> Option.bind f

        let assignIndexedElements magicCode f length =
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
        let EGTHeader = "GOLD Parser Tables/5.0"

    open Implementation

    let makeGrammar {Header = header; Records = records} =
        match header with
        | EGTHeader -> maybe {
            let! records = records |> List.map (fun (Record x) -> x) |> collectRecords

            let! properties =
                records
                |> Map.tryFind 'p'B
                |> Option.defaultValue [] // We don't care if there are no properties.
                |> List.map readProperty
                |> List.allSome
                |> Option.map (Map.ofList >> Properties)

            let! tableCounts = getSingleElement 't'B readTableCounts records

            let! charSets = assignIndexedElements 'c'B readCharSet tableCounts.CharSetTables records
            let fCharSet = uint32 >> SafeArray.getUnsafe charSets

            let! symbols = assignIndexedElements 'S'B readSymbol tableCounts.SymbolTables records
            let fSymbol = uint32 >> SafeArray.getUnsafe symbols

            let fGroup = Indexed.createWithKnownLength16 tableCounts.GroupTables
            let! groups = assignIndexedElements 'g'B (readGroup fSymbol fGroup) tableCounts.GroupTables records

            let! productions = assignIndexedElements 'R'B (readProduction fSymbol) tableCounts.ProductionTables records
            let fProduction = uint32 >> SafeArray.getUnsafe productions

            let fDFA = Indexed.createWithKnownLength16 tableCounts.DFATables
            let! dfaStates = assignIndexedElements 'D'B (readDFAState fCharSet fSymbol fDFA) tableCounts.DFATables records

            let fLALR = Indexed.createWithKnownLength16 tableCounts.LALRTables
            let! lalrStates = assignIndexedElements 'L'B (readLALRState fSymbol fProduction fLALR) tableCounts.LALRTables records

            return! None
        }
