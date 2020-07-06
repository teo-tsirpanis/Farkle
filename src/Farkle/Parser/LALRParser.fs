// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar
open Farkle.IO
open System
open System.Buffers
open System.Collections.Immutable

/// Functions to syntactically parse a series of tokens using the LALR algorithm.
module LALRParser =

    let private unloadStackIntoBuffer length stack =
        let arr = ArrayPool.Shared.Rent length
        let rec impl stack i =
            match stack with
            | (_, x) :: xs when i >= 0 ->
                Array.set arr i x
                impl xs (i - 1)
            | _ -> arr
        impl stack (length - 1)

    /// <summary>Parses and post-processes tokens with the LALR(1) algorithm.</summary>
    /// <param name="fMessage">A function that gets called for every event that happens.
    /// Useful for logging</param>
    /// <param name="lalrStates">The LALR state table to use.</param>
    /// <param name="oops">The <see cref="OptimizedOperations"/> object that will make the parsing faster.</param>
    /// <param name="pp">The <see cref="PostProcessor"/> to use on the newly-fused productions.</param>
    /// <param name="fToken">The a function that takes an <see cref="InputStream"/> and
    /// returns a <see cref="Token"/> (if input did not end) and its <see cref="Position"/>.</param>
    /// <param name="input">The <see cref="InputStream"/>.</param>
    /// <exception cref="ParseError">An error did happen. In Farkle, this exception is caught by the <see cref="RuntimeFarkle"/></exception>
    let parseLALR fMessage (lalrStates: ImmutableArray<LALRState>) (oops: OptimizedOperations) (pp: PostProcessor<'TResult>) fToken (input: CharStream) =
        let fail msg: obj = (input.LastTokenPosition, msg) |> Message |> ParserError |> raise
        let rec impl token currentState stack =
            let (|LALRState|) x = lalrStates.[int x]
            let nextAction =
                match token with
                | Some({Symbol = x}) -> oops.GetLALRAction x currentState
                | None -> currentState.EOFAction
            match nextAction with
            | Some LALRAction.Accept -> stack |> List.head |> snd
            | Some(LALRAction.Shift (LALRState nextState)) ->
                match token with
                | Some {Data = data} ->
                    fMessage <| ParseMessage.Shift nextState.Index
                    // We can't use <| because it prevents optimization into a loop.
                    // See https://github.com/dotnet/fsharp/issues/6984 for details.
                    impl (fToken input) nextState ((nextState, data) :: stack)
                | None -> failwithf "Error in state %d: the parser cannot emit shift when EOF is encountered." currentState.Index
            | Some(LALRAction.Reduce productionToReduce) ->
                let handleLength = productionToReduce.Handle.Length
                let stack' = List.skip handleLength stack
                /// The stack cannot be empty; we gave it one element in the beginning.
                let nextState = fst stack'.Head
                match oops.LALRGoto productionToReduce.Head nextState with
                | Some nextState ->
                    let resultObj =
                        let tokens = unloadStackIntoBuffer handleLength stack
                        try
                            pp.Fuse(productionToReduce, tokens)
                        finally
                            Array.Clear(tokens, 0, handleLength)
                            ArrayPool.Shared.Return(tokens)
                    fMessage <| ParseMessage.Reduction productionToReduce
                    impl token nextState ((nextState, resultObj) :: stack')
                | None -> failwithf "Error in state %d: GOTO was not found for production %O." nextState.Index productionToReduce
            | None ->
                let expectedSymbols =
                    currentState.Actions
                    |> Seq.map (fun (KeyValue(term, _)) -> ExpectedSymbol.Terminal term)
                    |> set
                    |> (fun s -> if currentState.EOFAction.IsSome then Set.add ExpectedSymbol.EndOfInput s else s)
                let foundToken =
                    match token with
                    | Some {Symbol = term} -> ExpectedSymbol.Terminal term
                    | None -> ExpectedSymbol.EndOfInput
                (expectedSymbols, foundToken) |> ParseErrorType.SyntaxError |> fail
        impl (fToken input) lalrStates.[0] [lalrStates.[0], null] :?> 'TResult
