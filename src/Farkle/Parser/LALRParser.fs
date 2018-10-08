// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Monads
open Farkle.PostProcessor

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal LALRParser =

    open State

    let private getNextAction currentState symbol = LALRState.actions currentState |> Map.tryFind symbol

    let private parseLALR {InitialState = initialState; States = lalrStates} (pp: PostProcessor<_>) token =
        let getCurrentState = List.tryHead >> Option.map fst >> Option.defaultValue initialState
        let (|LALRState|) x = SafeArray.retrieve lalrStates x
        let impl state =
            let nextAvailableActions = (getCurrentState state).Actions
            match nextAvailableActions.TryFind(token.Symbol), state with
            | Some Accept, (_, ast) :: _ -> Ok <| LALRResult.Accept ast, state
            | Some Accept, [] -> Error LALRStackEmpty, state
            | Some (Shift (LALRState nextState)), state -> Ok <| LALRResult.Shift nextState.Index, (nextState, pp.Transform token) :: state
            | Some (Reduce productionToReduce), state ->
                let tokens, state = List.popStack productionToReduce.Handle.Length <!> (Seq.map snd >> Array.ofSeq) <| state
                let nextState = getCurrentState state
                let nextAction = getNextAction nextState productionToReduce.Head
                match nextAction with
                | Some (Goto (LALRState nextState)) ->
                    let mutable resultObj = null
                    match pp.Fuse(productionToReduce, tokens, &resultObj) with
                    | true -> Ok <| LALRResult.Reduce productionToReduce, (nextState, resultObj) :: state
                    | false -> Error <| FuseError productionToReduce, state
                | _ -> Error <| GotoNotFoundAfterReduction (productionToReduce, nextState), state
            | Some (Goto _), _ | None, _ ->
                let expectedSymbols =
                    nextAvailableActions
                    |> Map.toSeq
                    |> Seq.map fst
                    |> Seq.filter (function | Terminal _ | EndOfFile | GroupStart _ | GroupEnd _ -> true | _ -> false)
                    |> List.ofSeq
                Ok <| LALRResult.SyntaxError (expectedSymbols, token.Symbol), state
        impl <!> tee id LALRResult.InternalError

    let create lalr pp =
        let rec impl currState token =
            let result, newState = State.run (parseLALR lalr pp token) currState
            result, (impl newState |> LALRParser)
        impl [] |> LALRParser