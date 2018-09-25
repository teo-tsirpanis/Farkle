// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Monads

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal LALRParser =

    open State

    let private getNextAction currentState symbol = LALRState.actions currentState |> Map.tryFind symbol

    let private parseLALR {InitialState = initialState; States = lalrStates} token =
        let getCurrentState = List.tryHead >> Option.map fst >> Option.defaultValue initialState
        let (|LALRState|) x = SafeArray.retrieve lalrStates x
        let impl state =
            let nextAvailableActions = (getCurrentState state).Actions
            match nextAvailableActions.TryFind(token.Symbol), state with
            | Some Accept, (_, ast) :: _ -> Ok <| LALRResult.Accept ast, state
            | Some Accept, [] -> Error LALRStackEmpty, state
            | Some (Shift (LALRState nextState)), state -> Ok <| LALRResult.Shift nextState.Index, (nextState, AST.Content token) :: state
            | Some (Reduce productionToReduce), state ->
                let tokens, state = List.popStack productionToReduce.Handle.Length <!> List.map snd <| state
                let nextState = getCurrentState state
                let nextAction = getNextAction nextState productionToReduce.Head
                match nextAction with
                | Some (Goto (LALRState nextState)) ->
                    let ast = AST.Nonterminal (productionToReduce, tokens)
                    Ok <| LALRResult.Reduce ast, (nextState, ast) :: state
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

    let create lalr =
        let rec impl currState token =
            let result, newState = State.run (parseLALR lalr token) currState
            result, (impl newState |> LALRParser)
        impl [] |> LALRParser