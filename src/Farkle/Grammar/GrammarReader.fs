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

        let readProperty fIndex =
            function
            | [UInt16 index; String name; String value] -> (name, value) |> IndexableWrapper.create16 index |> Some
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

        let readCharSet =
            let readRanges =
                function
                | UInt16 start :: UInt16 theEnd :: xs ->
                    (RangeSet.create (char start) (char theEnd), xs)
                    |> Some
                | _ -> None
                |> readToEnd false
                >> Option.map RangeSet.concat
            function
            | UInt16 index :: UInt16 _unicodePlane :: UInt16 _rangeCount
                :: Empty :: ranges ->
                ranges
                |> readRanges
                |> Option.map (IndexableWrapper.create16 index)
            | _ -> None

        let readSymbol =
            function
            | UInt16 index :: xs ->
                match xs with
                | [String name; UInt16 0us] -> Nonterminal (uint32 index, name) |> Some
                | [String name; UInt16 1us] -> Terminal (uint32 index, name) |> Some
                | [String name; UInt16 2us] -> Noise name |> Some
                | [String _ ; UInt16 3us] -> EndOfFile |> Some
                | [String name; UInt16 4us] -> GroupStart name |> Some
                | [String name; UInt16 5us] -> GroupEnd name |> Some
                | [String _; UInt16 7us] -> Error |> Some
                | _ -> None
                |> Option.map (IndexableWrapper.create16 index)
            | _ -> None

        let readGroup fSymbol fGroup =
            let readNestedGroups =
                (function | UInt16 x :: xs -> x |> fGroup |> Option.map (fun x -> x, xs) | _ -> None)
                |> readToEnd true
                >> Option.map set
            function
            | UInt16 index :: String name :: UInt16 containerIndex :: UInt16 startIndex :: UInt16 endIndex :: UInt16 advanceMode :: UInt16 endingMode :: Empty :: UInt16 _nestingCount :: xs -> maybe {
                let! containerSymbol = fSymbol containerIndex
                let! startSymbol = fSymbol startIndex
                let! endSymbol = fSymbol endIndex
                let! advanceMode = match advanceMode with | 0us -> Some Token | 1us -> Some Character | _ -> None
                let! endingMode = match endingMode with | 0us -> Some Open | 1us -> Some Closed | _ -> None
                let! nesting = readNestedGroups xs
                return {
                    Name = name
                    Index = uint32 index
                    ContainerSymbol = containerSymbol
                    StartSymbol = startSymbol
                    EndSymbol = endSymbol
                    AdvanceMode = advanceMode
                    EndingMode = endingMode
                    Nesting = nesting
                }
                }
            | _ -> None

        let readProduction fSymbol =
            let readChildrenSymbols =
                (function | UInt16 x :: xs -> x |> fSymbol |> Option.map (fun x -> x, xs) | _ -> None)
                |> readToEnd true
            function
            | UInt16 index :: UInt16 headIndex :: Empty :: xs -> maybe {
                let! headSymbol = fSymbol headIndex
                let! symbols = readChildrenSymbols xs
                return {Index = uint32 index; Head = headSymbol; Handle = symbols}
                }
            | _ -> None

        let readInitialStates =
            function
            | [UInt16 dfa; UInt16 lalr] -> (dfa, lalr) |> Some
            | _ -> None

        let readDFAState fCharSet fSymbol fDFA =
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
            | UInt16 index :: Boolean false :: UInt16 _ :: Empty :: xs ->
                xs
                |> readDFAEdges
                |> Option.map (fun edges -> (uint32 index, edges) |> DFAContinue)
            | UInt16 index :: Boolean true :: UInt16 acceptIndex :: Empty :: xs -> maybe {
                let! edges = readDFAEdges xs
                let! acceptSymbol = fSymbol acceptIndex
                let index = uint32 index
                return DFAAccept (index, (acceptSymbol, edges))
                }
            | _ -> None

        let readLALRState fSymbol fProduction fLALR =
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
            | UInt16 index :: Empty :: xs -> readLALRAction xs |> Option.map (fun actions -> {Index = uint32 index; Actions = actions})
            | _ -> None


    let makeGrammar {Header = header; Records = records} = failwith "To be rewritten"
