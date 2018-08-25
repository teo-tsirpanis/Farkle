// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Monads

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal LALRParser =

    open StateResult

    type LALRParserState =
        private {
            CurrentLALRState: LALRState
            LALRStack: (LALRState * AST) list
        }
        with
            static member Create state =
                {
                    CurrentLALRState = state
                    LALRStack = [state, Error |> Token.dummy |> AST.Content]
                }

    // These lenses must be hidden from the rest of the code
    let private CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
    let private LALRStack_ :Lens<_, _> = (fun x -> x.LALRStack), (fun v x -> {x with LALRStack = v})
    let getLALRStackTop =
            getOptic (LALRStack_ >-> List.head_)
            >>= (failIfNone LALRStackEmpty >> liftResult)
    let getCurrentLALR = getOptic CurrentLALRState_
    let setCurrentLALR = setOptic CurrentLALRState_
    let getNextAction currentState symbol =
        LALRState.actions currentState
        |> Map.tryFind symbol

    let private parseLALR lalrStates token = State.state {
        let (StateResult impl) = sresult {
            let getStateFromIndex = SafeArray.retrieve lalrStates
            let! currentState = getCurrentLALR
            let nextAvailableActions = LALRState.actions currentState
            match nextAvailableActions.TryFind(token.Symbol) with
            | Some (Accept) ->
                let! topAST = getLALRStackTop <!> snd
                return LALRResult.Accept topAST
            | Some (Shift nextState) ->
                let nextState = getStateFromIndex nextState
                do! setCurrentLALR nextState
                do! mapOptic LALRStack_ (List.cons (nextState, AST.Content token))
                return LALRResult.Shift nextState.Index
            | Some (Reduce x) ->
                let! head, result = sresult {
                    let count = x.Handle.Length
                    let popStack optic count = sresult {
                        let! (first, rest) = getOptic optic <!> List.splitAt count
                        do! setOptic optic rest
                        return first
                    }
                    let! tokens =
                        popStack LALRStack_ count
                        <!> (Seq.map snd >> Seq.rev >> List.ofSeq)
                    let ast = AST.Nonterminal (x, tokens)
                    return (currentState, ast), ReduceNormal ast
                }
                let! newState = getLALRStackTop <!> fst
                let nextAction = getNextAction newState x.Head
                match nextAction with
                | Some (Goto nextState) ->
                    let nextState = getStateFromIndex nextState
                    do! setCurrentLALR nextState
                    let head = nextState, snd head
                    do! mapOptic (LALRStack_) (List.cons head)
                | _ -> do! fail <| GotoNotFoundAfterReduction (x, newState.Index)
                return result
            | Some (Goto _) | None ->
                let expectedSymbols =
                    nextAvailableActions
                    |> Map.toSeq
                    |> Seq.map fst
                    |> Seq.filter (function | Terminal _ | EndOfFile | GroupStart _ | GroupEnd _ -> true | _ -> false)
                    |> List.ofSeq
                return LALRResult.SyntaxError (expectedSymbols, token.Symbol)
        }
        let! x = impl
        match x with
        | Ok x -> return x
        | Result.Error x -> return LALRResult.InternalError x
    }

    let create {InitialState = initialState; States = states} =
        let f = parseLALR states
        let rec impl currState token =
            let result, newState = State.run (f token) currState
            result, (impl newState |> LALRParser)
        impl (LALRParserState.Create initialState) |> LALRParser