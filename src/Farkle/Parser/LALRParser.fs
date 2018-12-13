// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar2
open Farkle.Monads
open Farkle.PostProcessor

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal LALRParser =

    open State

    let private parseLALR {InitialState = initialState; States = lalrStates} (pp: PostProcessor<_>) fNextToken =
        let getCurrentState = List.tryHead >> Option.map fst >> Option.defaultValue initialState
        let (|LALRState|) x = SafeArray.retrieve lalrStates x
        let rec impl state = either {
            let currentState = (getCurrentState state)
            let! token = fNextToken()
            match currentState.Actions.TryFind(Option.map (fun {Symbol = x} -> x) token), state with
            | Some LALRAction.Accept, (_, ast) :: _ -> return ast
            | Some LALRAction.Accept, [] -> return! Error <| LALRResult.InternalError LALRStackEmpty
            | Some (LALRAction.Shift (LALRState nextState)), state ->
                match token with
                | Some {Data = data} -> return! impl <| (nextState, data) :: state
                | None -> return! Error <| LALRResult.InternalError ShiftOnEOF
            | Some (LALRAction.Reduce productionToReduce), state ->
                let tokens, state = List.popStack productionToReduce.Handle.Length <!> (Seq.map snd >> Array.ofSeq) <| state
                let nextState = getCurrentState state
                let nextAction = nextState.GotoActions.TryFind productionToReduce.Head
                match nextAction with
                | Some (LALRState nextState) ->
                    match pp.Fuse productionToReduce tokens () with
                    | true, resultObj -> return! impl <| (nextState, resultObj) :: state
                    | false, _ -> return! productionToReduce |> FuseError |> LALRResult.InternalError |> Error
                | _ -> return! (productionToReduce, nextState) |> GotoNotFoundAfterReduction |> LALRResult.InternalError |> Error
            | None, _ ->
                let fixTerminal = Option.map ExpectedSymbol.Terminal >> Option.defaultValue ExpectedSymbol.EndOfInput
                let expectedSymbols =
                    [
                        currentState.Actions
                        |> Map.toSeq
                        |> Seq.map (fst >> fixTerminal)

                        currentState.GotoActions
                        |> Map.toSeq
                        |> Seq.map (fst >> ExpectedSymbol.Nonterminal)
                    ]
                    |> Seq.concat
                    |> set
                return! Error <| LALRResult.SyntaxError (expectedSymbols, token |> Option.map (fun {Symbol = x} -> x) |> fixTerminal)
        }
        impl []

    let create lalr pp =
        let rec impl currState token =
            let result, newState = State.run (parseLALR lalr pp token) currState
            result, (impl newState |> LALRParser)
        impl [] |> LALRParser