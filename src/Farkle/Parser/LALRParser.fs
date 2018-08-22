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
            LALRStack: (Token * (LALRState * Reduction option)) list
        }
        with
            static member Create state =
                {
                    CurrentLALRState = state
                    LALRStack = [Token.dummy Error, (state, None)]
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
                let! topReduction = getLALRStackTop >>= (snd >> snd >> failIfNone ReductionNotFoundOnAccept >> liftResult)
                return LALRResult.Accept topReduction
            | Some (Shift nextState) ->
                let nextState = getStateFromIndex nextState
                do! setCurrentLALR nextState
                do! mapOptic LALRStack_ (List.cons (token, (nextState, None)))
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
                        <!> (Seq.map (function | (x, (_, None)) -> Choice1Of2 x | (_, (_, Some x)) -> Choice2Of2 x) >> Seq.rev >> List.ofSeq)
                    let reduction = {Tokens = tokens; Parent = x}
                    let token = {Symbol = x.Head; Position = Position.initial; Data = reduction.ToString()}
                    let head = token, (currentState, Some reduction)
                    return head, ReduceNormal reduction
                }
                let! newState = getLALRStackTop <!> (snd >> fst)
                let nextAction = getNextAction newState x.Head
                match nextAction with
                | Some (Goto nextState) ->
                    let nextState = getStateFromIndex nextState
                    do! setCurrentLALR nextState
                    let! head = getCurrentLALR <!> (fun currentLALR -> fst head, (currentLALR, head |> snd |> snd))
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