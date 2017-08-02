// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads

module Parser =

    open State

    let getLookAheadBuffer n x =
        let n = System.Math.Min(int n, Collections.List.length x)
        let x = x |> StateResult.eval (List.take n)
        match x with
        | Ok (x, _) -> x |> String.ofList
        | Bad _ -> ""

    let consumeBuffer n = state {
        let! len = getOptic ParserState.InputStream_ <!> Collections.List.length
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
        | n when n > 0 && n <= len ->
            return! repeatM consumeSingle n |> ignore
        | _ -> do ()
    }

    // Pascal code (ported from Java 💩): 72 lines of begin/ends, mutable hell and unreasonable garbage.
    // F# code: 22 lines of clear, reasonable and type-safe code. I am so confident and would not even test it!
    // This is a 30.5% decrease of code and a 30.5% increase of productivity. Why do __You__ still code in C# (☹)? Or Java (😠)?
    let tokenizeDFA dfaStates initialState pos input =
        let rec impl currPos currState lastAccept lastAccPos x =
            let newToken = Token.Create pos
            let newPos = currPos + 1u
            match x with
            | [] -> newToken Symbol.EOF ""
            | x :: xs ->
                let newDFA =
                    currState
                    |> DFAState.edges
                    |> Set.toSeq
                    |> Seq.tryFind (fun (cs, _) -> RangeSet.contains cs x)
                    |> Option.bind (snd >> Indexed.getfromList dfaStates >> Trial.makeOption)
                match newDFA with
                | Some dfa ->
                    match dfa.AcceptSymbol with
                    | Some x -> impl newPos dfa (Some x) currPos xs
                    | None -> impl newPos dfa lastAccept lastAccPos xs
                | None ->
                    match lastAccept with
                    | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x
                    | None -> input |> getLookAheadBuffer 1u |> newToken Symbol.Error
        impl 1u initialState None 0u input

    let inline tokenizeDFAForDummies state =
        let grammar = state |> ParserState.grammar
        tokenizeDFA grammar.DFAStates grammar.InitialStates.DFA state.CurrentPosition state.InputStream

    let rec produceToken() = state {
        let! x = get <!> tokenizeDFAForDummies
        let! grammar = get <!> ParserState.grammar
        let! groupStackTop = getOptic (ParserState.GroupStack_ >-> List.head_)
        let nestGroup =
            match x ^. Token.Symbol_ |> Symbol.symbolType with
            | GroupStart | GroupEnd ->
                Maybe.maybe {
                    let! groupStackTop = groupStackTop
                    let! gsTopGroup = groupStackTop ^. Token.Symbol_ |> Group.getSymbolGroup grammar.Groups
                    let! myIndex = x ^. Token.Symbol_ |> Group.getSymbolGroupIndexed grammar.Groups
                    return gsTopGroup.Nesting.Contains myIndex
                } |> Option.defaultValue true
            | _ -> false
        if nestGroup then
            do! x ^. Token.Data_ |> String.length |> consumeBuffer
            let newToken = Optic.set Token.Data_ "" x
            do! mapOptic ParserState.GroupStack_ (cons newToken)
            return! produceToken()
        else
            match groupStackTop with
            | None ->
                do! x ^. Token.Data_ |> String.length |> consumeBuffer
                return x
            | Some groupStackTop ->
                let groupStackTopGroup =
                    groupStackTop ^. Token.Symbol_
                    |> Group.getSymbolGroup grammar.Groups
                    |> mustBeSome // I am sorry. 😭
                if groupStackTopGroup.EndSymbol = x.Symbol then
                    let! pop = state {
                        do! mapOptic ParserState.GroupStack_ List.tail
                        if groupStackTopGroup.EndingMode = Closed then
                            do! x ^. Token.Data_ |> String.length |> consumeBuffer
                            return groupStackTop |> Token.AppendData x.Data
                        else
                            return groupStackTop
                    }
                    let! groupStackTop = getOptic (ParserState.GroupStack_ >-> List.head_)
                    match groupStackTop with
                        | Some _ ->
                            do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                            return! produceToken()
                        | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                elif x.Symbol.SymbolType = EndOfFile then
                    return x
                else
                    match groupStackTopGroup.AdvanceMode with
                    | Token ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (Token.AppendData x.Data)
                        do! x ^. Token.Data_ |> String.length |> consumeBuffer
                    | Character ->
                        do! mapOptic (ParserState.GroupStack_ >-> List.head_) (x.Data |> Seq.head |> sprintf "%c" |> Token.AppendData)
                        do! consumeBuffer 1
                    return! produceToken()
    }

    open StateResult

    let parseLALR token = state {
        let (StateResult impl) = sresult {
            let! grammar = get <!> ParserState.grammar
            let states = grammar.LALRStates
            let lalrStackTop =
                getOptic (ParserState.LALRStack_ >-> List.head_)
                >>= (failIfNone LALRStackEmpty >> liftResult)
            let setCurrentLALR x =
                Indexed.getfromList states x
                |> liftResult
                |> mapFailure ParseError.IndexNotFound
                >>= setOptic ParserState.CurrentLALRState_
            let! (LALRState currentState) = getOptic ParserState.CurrentLALRState_
            match currentState.TryFind (token ^. Token.Symbol_) with
            | Some (Accept) -> return LALRResult.Accept
            | Some (Shift x) ->
                do! setCurrentLALR x
                do! mapOptic ParserState.LALRStack_ (cons (token, (LALRState currentState, None)))
                return LALRResult.Shift
            | Some (Reduce x) ->
                let! head, result = sresult {
                    let! shouldTrim = get <!> (ParserState.trimReductions >> ((&&) (Production.hasOneNonTerminal x)))
                    if shouldTrim then
                        let! head = lalrStackTop
                        do! mapOptic ParserState.LALRStack_ List.tail
                        let head = Optic.set (Optics.fst_ >-> Token.Symbol_) x.Nonterminal head
                        return head, ReduceEliminated
                    else
                        let count = x.Symbols.Length
                        let popStack optic = sresult {
                            let! x = getOptic optic
                            match x with
                            | x :: xs ->
                                do! setOptic optic xs
                                return Some x
                            | [] -> return None
                        }
                        let! tokens =
                            repeatM (popStack ParserState.LALRStack_) count
                            <!> (Seq.choose id >> Seq.map fst >> Seq.rev >> List.ofSeq)
                        let reduction = {Tokens = tokens; Parent = x}
                        let token = {Symbol = x.Nonterminal; Position = Position.initial; Data = Reduction.data reduction}
                        let head = token, (LALRState currentState, Some reduction)
                        return head, ReduceNormal
                }
                let! (LALRState newState) = lalrStackTop <!> (snd >> fst)
                match newState.TryFind x.Nonterminal with
                | Some (Goto x) ->
                    do! setCurrentLALR x
                    let head = fst head, (LALRState newState, (head |> snd |> snd))
                    do! mapOptic (ParserState.LALRStack_) (cons head)
                | _ -> do! fail GotoNotFoundAfterReduction
                return result
            | Some (Goto _) | None ->
                return
                    currentState
                    |> Map.toList
                    |> List.map fst
                    |> List.filter
                        (Symbol.symbolType >> (function | Terminal | EndOfFile | GroupStart | GroupEnd -> true | _ -> false))
                    |> LALRResult.SyntaxError
        }
        let! x = impl
        match x with
        | Ok (x, _) -> return x
        | Bad x -> return x |> LALRResult.InternalErrors
    }

    open State

    let rec parse() = state {
        let! tokens = getOptic ParserState.InputStack_
        let! isGroupStackEmpty = getOptic ParserState.GroupStack_ <!> List.isEmpty
        match tokens with
        | [] ->
            let! newToken = produceToken()
            do! mapOptic ParserState.InputStack_ (cons newToken)
            if newToken.Symbol.SymbolType = EndOfFile && not isGroupStackEmpty then
                return GroupError
            else
                return TokenRead
        | newToken :: xs ->
            match newToken.Symbol.SymbolType with
            | Noise ->
                do! setOptic ParserState.InputStack_ xs
                return! parse()
            | Error -> return LexicalError newToken
            | EndOfFile when not isGroupStackEmpty -> return GroupError
            | _ ->
                let! lalrResult = parseLALR newToken
                match lalrResult with
                | LALRResult.Accept -> return ParseMessage.Accept
                | LALRResult.Shift ->
                    do! mapOptic ParserState.InputStack_ List.skipLast
                    return! parse()
                | ReduceNormal -> return ParseMessage.Reduction
                | ReduceEliminated -> return! parse()
                | LALRResult.SyntaxError x -> return ParseMessage.SyntaxError x
                | LALRResult.InternalErrors x -> return ParseMessage.InternalErrors x
    }
