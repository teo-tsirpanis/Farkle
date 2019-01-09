// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.PostProcessor

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Functions to syntactically parse a series of tokens using the LALR algorithm.
module LALRParser =

    let private mapState f m (s: 'c) =
        let (x, s: 'c) = m s
        (f x, s)

    /// Parses and post-processes tokens based on a `Grammar`.
    /// This function accepts:
    /// 1. a function to report any significant _parsing-related_ messages (`TokenRead` events will not be fired from here).
    /// 2. the `Grammar` to use.
    /// 3. the `PostProcessor` to use on the newly-fused productions.
    /// 4. the current position (redundant if the input did not end).
    /// 5. the token that was found (or `None` if input ended).
    /// 6. the LALR parser's stack state. Pass an empty list on the first try.
    /// This function returns:
    /// 1. if parsing finished, the final fused production, or an error type, or `None` if parsing did not finish.
    /// 2. the new stack.
    let LALRStep fMessage {_LALRStates = {InitialState = initialState; States = lalrStates}} (pp: PostProcessor<_>) pos token stack =
        let fail msg = Message(pos, msg) |> Error |> Some
        let internalError = ParseErrorType.InternalError >> fail
        let getCurrentState = List.tryHead >> Option.map fst >> Option.defaultValue initialState
        let (|LALRState|) x = SafeArray.retrieve lalrStates x
        let rec impl stack =
            let currentState = getCurrentState stack
            match currentState.Actions.TryFind(Option.map (fun {Symbol = x} -> x) token), stack with
            | Some LALRAction.Accept, (_, ast) :: _ -> Some <| Ok ast, stack
            | Some LALRAction.Accept, [] -> LALRStackEmpty |> internalError, stack
            | Some (LALRAction.Shift (LALRState nextState)), stack ->
                match token with
                | Some {Data = data} ->
                    fMessage <| ParseMessage.Shift nextState.Index
                    None, (nextState, data) :: stack
                | None -> ShiftOnEOF |> internalError, stack
            | Some (LALRAction.Reduce productionToReduce), stack ->
                let tokens, stack = List.popStack productionToReduce.Handle.Length |> mapState (Seq.map snd >> Array.ofSeq) <| stack
                let nextState = getCurrentState stack
                let nextAction = nextState.GotoActions.TryFind productionToReduce.Head
                match nextAction with
                | Some (LALRState nextState) ->
                    try
                        let resultObj = pp.Fuse(productionToReduce, tokens)
                        fMessage <| ParseMessage.Reduction productionToReduce
                        impl <| (nextState, resultObj) :: stack
                    with
                    | ex -> FuseError(productionToReduce, ex) |> internalError, stack
                | _ -> GotoNotFoundAfterReduction (productionToReduce, nextState) |> internalError, stack
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
                (expectedSymbols, token |> Option.map (fun {Symbol = x} -> x) |> fixTerminal) |> ParseErrorType.SyntaxError |> fail, stack
        impl stack
