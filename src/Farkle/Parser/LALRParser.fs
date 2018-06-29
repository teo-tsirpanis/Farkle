// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Grammar
open Farkle.Monads

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal LALRParser =

    open StateResult

    type LALRParserState =
        private {
            CurrentLALRState: uint32
            LALRStack: (Token * (uint32 * Reduction option)) list
        }
        with
            static member Create (lalr: LALR) =
                {
                    CurrentLALRState = lalr.InitialState
                    LALRStack = [Token.dummy Error, (lalr.InitialState, None)]
                }

    // These lenses must be hidden from the rest of the code
    let private CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
    let private LALRStack_ :Lens<_, _> = (fun x -> x.LALRStack), (fun v x -> {x with LALRStack = v})

    let private parseLALR lalrStates token = State.state {
        let (StateResult impl) = sresult {
            let lalrStackTop =
                getOptic (LALRStack_ >-> List.head_)
                >>= (failIfNone LALRStackEmpty >> liftResult)
            let getNextActions currIndex = lalrStates.States.TryFind currIndex |> failIfNone (LALRStateIndexNotFound currIndex) |> liftResult
            let getNextAction currIndex symbol =
                getNextActions currIndex
                <!> (Map.tryFind symbol)
            let getCurrentLALR = getOptic CurrentLALRState_
            let setCurrentLALR = setOptic CurrentLALRState_
            let! currentState = getCurrentLALR
            let! nextAvailableActions = getNextActions currentState
            match nextAvailableActions.TryFind(token.Symbol) with
            | Some (Accept) ->
                let! topReduction = lalrStackTop <!> (snd >> snd >> mustBeSome) // I am sorry. 😭
                return LALRResult.Accept topReduction
            | Some (Shift x) ->
                do! setCurrentLALR x
                do! getCurrentLALR >>= (fun x -> mapOptic LALRStack_ (List.cons (token, (x, None))))
                return LALRResult.Shift x
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
                let! newState = lalrStackTop <!> (snd >> fst)
                let! nextAction = getNextAction newState x.Head
                match nextAction with
                | Some (Goto x) ->
                    do! setCurrentLALR x
                    let! head = getCurrentLALR <!> (fun currentLALR -> fst head, (currentLALR, head |> snd |> snd))
                    do! mapOptic (LALRStack_) (List.cons head)
                | _ -> do! fail <| GotoNotFoundAfterReduction (x, newState)
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

    let rec create lalr =
        let f = parseLALR lalr
        let rec impl currState token =
            let result, newState = State.run (f token) currState
            result, (impl newState |> LALRParser)
        impl (LALRParserState.Create lalr) |> LALRParser