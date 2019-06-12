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

    /// Parses and post-processes tokens based on a `Grammar` and a `PostProcessor`, until parsing completes.
    /// This function accepts:
    /// 1. a function to report any significant _parsing-related_ messages (`TokenRead` events will not be fired from here).
    /// 2. the `Grammar` to use.
    /// 3. the `PostProcessor` to use on the newly-fused productions.
    /// 4. The a function that takes an input and returns a token (if input did not end) and its position.
    /// 5. An arbitrary argument to the above function.
    let parseLALR fMessage {_LALRStates = {InitialState = initialState; States = lalrStates}} (pp: PostProcessor<_>) fToken =
        let getCurrentState stack =
            match stack with
            | (x, _) :: _ -> x
            | [] -> initialState
        let (|LALRState|) x = SafeArray.retrieve lalrStates x
        let rec impl (pos, token as t) stack =
            let fail msg: obj = Message(pos, msg) |> ParseError |> raise
            let internalError msg: obj = msg |> ParseErrorType.InternalError |> fail
            let currentState = getCurrentState stack
            match currentState.Actions.TryFind(Option.map (fun {Symbol = x} -> x) token), stack with
            | Some LALRAction.Accept, (_, ast) :: _ -> ast
            | Some LALRAction.Accept, [] -> LALRStackEmpty |> internalError
            | Some (LALRAction.Shift (LALRState nextState)), stack ->
                match token with
                | Some {Data = data} ->
                    fMessage <| ParseMessage.Shift nextState.Index
                    // We can't use <| because it prevents optimization into a loop.
                    // See https://github.com/dotnet/fsharp/issues/6984 for details.
                    impl (fToken()) ((nextState, data) :: stack)
                | None -> ShiftOnEOF |> internalError
            | Some (LALRAction.Reduce productionToReduce), stack ->
                let tokens, stack =
                    let (poppedStack, remainingStack) = List.popStack productionToReduce.Handle.Length stack
                    poppedStack |> Seq.map snd |> Array.ofSeq, remainingStack
                let nextState = getCurrentState stack
                let nextAction = nextState.GotoActions.TryFind productionToReduce.Head
                match nextAction with
                | Some (LALRState nextState) ->
                    let resultObj = 
                        try
                            pp.Fuse(productionToReduce, tokens)
                        with
                        | FuserNotFound prod -> ParseErrorType.FuserNotFound prod |> fail
                        | ex -> ParseErrorType.FuseError(productionToReduce, ex) |> fail
                    fMessage <| ParseMessage.Reduction productionToReduce
                    impl t ((nextState, resultObj) :: stack)
                | _ -> GotoNotFoundAfterReduction (productionToReduce, nextState) |> internalError
            | None, _ ->
                let fixTerminal =
                    function
                    | Some term, _ -> ExpectedSymbol.Terminal term
                    | None, _ -> ExpectedSymbol.EndOfInput
                let expectedSymbols =
                    [
                        currentState.Actions
                        |> Map.toSeq
                        |> Seq.map fixTerminal

                        currentState.GotoActions
                        |> Map.toSeq
                        |> Seq.map (fst >> ExpectedSymbol.Nonterminal)
                    ]
                    |> Seq.concat
                    |> set
                let foundToken =
                    match token with
                    | Some {Symbol = term} -> ExpectedSymbol.Terminal term
                    | None -> ExpectedSymbol.EndOfInput
                (expectedSymbols, foundToken) |> ParseErrorType.SyntaxError |> fail
        impl (fToken()) []
