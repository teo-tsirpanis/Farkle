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

    open State

    let getLookAheadBuffer n x =
        let n = System.Math.Min(int n, List.length x)
        x |> List.takeSafe n |> String.ofList

    let consumeBuffer n = state {
        let consumeSingle = state {
            let! x = getOptic ParserState.InputStream_
            match x with
            | x :: xs ->
                do! setOptic ParserState.InputStream_ xs
                match x with
                | LF ->
                    let! c = getOptic ParserState.CurrentPosition_ <!> Position.column
                    if c > 1u then
                        do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | CR -> do! mapOptic ParserState.CurrentPosition_ Position.incLine
                | _ -> do! mapOptic ParserState.CurrentPosition_ Position.incCol
            | [] -> do ()
        }
        match n with
        | n when n > 0 ->
            return! repeatM consumeSingle n |> ignore
        | _ -> do ()
    }

    // Pascal code (ported from Java 💩): 72 lines of begin/ends, mutable hell and unreasonable garbage.
    // F# code: 22 lines of clear, reasonable and type-safe code. I am so confident and would not even test it!
    // This is a 30.5% decrease of code and a 30.5% increase of productivity. Why do __You__ still code in C# (☹)? Or Java (😠)?
    let tokenizeDFA {Transition = trans; InitialState = initialState; AcceptStates = accStates} pos input =
        let newToken = Token.Create pos
        let rec impl currPos currState lastAccept lastAccPos x =
            let newPos = currPos + 1u
            match x with
            | [] ->
                match lastAccept with
                | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x |> Ok
                | None -> newToken EndOfFile "" |> Ok
            | x :: xs ->
                let newDFA =
                    trans.TryFind(currState)
                    |> Option.bind (fun m -> m |> Map.toSeq |> Seq.tryFind (fun (cs, _) -> RangeSet.contains cs x))
                    |> Option.map snd
                match newDFA with
                | Some dfa ->
                    match accStates.TryFind(dfa) with
                    | Some sym -> impl newPos dfa (Some sym) currPos xs
                    | None -> impl newPos dfa lastAccept lastAccPos xs
                | None ->
                    match lastAccept with
                    | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x |> Ok
                    | None -> input |> getLookAheadBuffer 1u |> Core.Error
        impl 1u initialState None 0u input

    let inline tokenizeDFAForDummies dfa state =
        tokenizeDFA dfa state.CurrentPosition state.InputStream

    open StateResult

    let rec produceToken dfa groups = sresult {
        let consumeBuffer = consumeBuffer >> liftState
        let! x = get <!> (tokenizeDFAForDummies dfa >> liftResult) >>= id
        let! groupStackTop = getOptic ParserState.GroupStack_ <!> List.tryHead
        let nestGroup =
            match x.Symbol with
            | GroupStart _ | GroupEnd _ ->
                Maybe.maybe {
                    let! groupStackTop = groupStackTop
                    let! gsTopGroup = groupStackTop.Symbol |> Group.getSymbolGroup groups
                    let! myIndex = x.Symbol |> Group.getSymbolGroupIndexed groups
                    return gsTopGroup.Nesting.Contains myIndex
                } |> Option.defaultValue true
            | _ -> false
        if nestGroup then
            do! x.Data |> String.length |> consumeBuffer
            let newToken = Optic.set Token.Data_ "" x
            do! mapOptic ParserState.GroupStack_ (List.cons newToken)
            return! produceToken dfa groups
        else
            match groupStackTop with
            | None ->
                do! x.Data |> String.length |> consumeBuffer
                return x
            | Some groupStackTop ->
                let groupStackTopGroup =
                    groupStackTop.Symbol
                    |> Group.getSymbolGroup groups
                    |> mustBeSome // I am sorry. 😭
                if groupStackTopGroup.EndSymbol = x.Symbol then
                    let! pop = sresult {
                        do! mapOptic ParserState.GroupStack_ List.tail
                        if groupStackTopGroup.EndingMode = Closed then
                            do! x.Data |> String.length |> consumeBuffer
                            return groupStackTop |> Token.AppendData x.Data
                        else
                            return groupStackTop
                    }
                    let! groupStackTop = getOptic (ParserState.GroupStack_ >-> List.head_)
                    match groupStackTop with
                        | Some _ ->
                            do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                            return! produceToken dfa groups
                        | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                elif x.Symbol = EndOfFile then
                    return x
                else
                    match groupStackTopGroup.AdvanceMode with
                    | Token ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData x.Data)
                        do! x.Data |> String.length |> consumeBuffer
                    | Character ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (x.Data.[0] |> string |> Token.AppendData)
                        do! consumeBuffer 1
                    return! produceToken dfa groups
    }

    let parseLALR lalrStates token = state {
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

    let rec stepParser (grammar: Grammar) p =
        let rec impl() = state {
            let! tokens = State.getOptic ParserState.InputStack_
            let! isGroupStackEmpty = State.getOptic ParserState.GroupStack_ |> State.map List.isEmpty
            match tokens with
            | [] ->
                let! newToken = produceToken grammar.DFA grammar.Groups |> (fun (StateResult x) -> x)
                match newToken with
                | Ok newToken ->
                    do! State.mapOptic ParserState.InputStack_ (List.cons newToken)
                    if newToken.Symbol = EndOfFile && not isGroupStackEmpty then
                        return GroupError
                    else
                        return TokenRead newToken
                | Result.Error x -> return LexicalError x.[0]
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
        let state = ParserState.create grammar input
        stepParser grammar state
