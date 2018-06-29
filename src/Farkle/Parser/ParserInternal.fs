// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Grammar
open Farkle.Monads

module internal Internal =

    open StateResult

    let parseLALR lalrStates token = State.state {
        let (StateResult impl) = sresult {
            let lalrStackTop =
                getOptic (ParserState.LALRStack_ >-> List.head_)
                >>= (failIfNone LALRStackEmpty >> liftResult)
            let getNextActions currIndex = lalrStates.States.TryFind currIndex |> failIfNone (LALRStateIndexNotFound currIndex) |> liftResult
            let getNextAction currIndex symbol =
                getNextActions currIndex
                <!> (Map.tryFind symbol)
            let getCurrentLALR = getOptic ParserState.CurrentLALRState_
            let setCurrentLALR = setOptic ParserState.CurrentLALRState_
            let! currentState = getCurrentLALR
            let! nextAvailableActions = getNextActions currentState
            match nextAvailableActions.TryFind(token.Symbol) with
            | Some (Accept) ->
                let! topReduction = lalrStackTop <!> (snd >> snd >> mustBeSome) // I am sorry. 😭
                return LALRResult.Accept topReduction
            | Some (Shift x) ->
                do! setCurrentLALR x
                do! getCurrentLALR >>= (fun x -> mapOptic ParserState.LALRStack_ (List.cons (token, (x, None))))
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
                        popStack ParserState.LALRStack_ count
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
                    do! mapOptic (ParserState.LALRStack_) (List.cons head)
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

    let tokenize = State.state {
        let! tokenizer = State.getOptic ParserState.TheTokenizer_
        match tokenizer.Value with
        | EndlessProcess (x, xs) ->
            do! State.setOptic ParserState.TheTokenizer_ xs
            do! State.setOptic ParserState.CurrentPosition_ x.CurrentPosition
            do! State.setOptic ParserState.IsGroupStackEmpty_ x.IsGroupStackEmpty
            return x.NewToken
    }

    let rec stepParser (grammar: Grammar) p =
        let rec impl() = State.state {
            let! tokens = State.getOptic ParserState.InputStack_
            let! isGroupStackEmpty = State.getOptic ParserState.IsGroupStackEmpty_
            match tokens with
            | [] ->
                let! newToken = tokenize
                do! State.setOptic ParserState.InputStack_ [newToken]
                if newToken.Symbol = EndOfFile && not isGroupStackEmpty then
                    return GroupError
                else
                    return TokenRead newToken
            | newToken :: xs ->
                match newToken.Symbol with
                | Noise _ ->
                    do! State.setOptic ParserState.InputStack_ xs
                    return! impl()
                | Error -> return LexicalError newToken.Data.[0]
                | EndOfFile when not isGroupStackEmpty -> return GroupError
                | _ ->
                    let! lalrResult = parseLALR grammar.LALR newToken
                    match lalrResult with
                    | LALRResult.Accept x -> return ParseMessageType.Accept x
                    | LALRResult.Shift x ->
                        do! State.mapOptic ParserState.InputStack_ List.skipLast
                        return ParseMessageType.Shift x
                    | ReduceNormal x -> return Reduction x
                    | LALRResult.SyntaxError (x, y) -> return SyntaxError (x, y)
                    | LALRResult.InternalError x -> return InternalError x
        }
        let (result, nextState) = State.run (impl()) p
        let makeMessage = nextState.CurrentPosition |> ParseMessage.Create
        match result with
        | ParseMessageType.Accept x -> Parser.Finished (x |> ParseMessageType.Accept |> makeMessage, x)
        | x when x.IsError -> x |> makeMessage |> Parser.Failed
        | x -> Parser.Continuing (makeMessage x, lazy (stepParser grammar nextState))

    let createParser grammar input =
        let state = ParserState.create grammar (TokenizerImpl.create grammar.DFA grammar.Groups input)
        stepParser grammar state
