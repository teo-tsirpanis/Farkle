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

    type private ObjectBuffer() =
        let mutable arr = ArrayPool.Shared.Rent 16
        let ensureSized newSize =
            if newSize > arr.Length then
                ArrayPool.Shared.Return arr
                arr <- ArrayPool.Shared.Rent newSize
        member _.GetBufferFromStack length stack =
            ensureSized length
            let mutable i = length - 1
            let mutable stack = stack
            // Remember: "a > b" in boolean means "a && (not b)".
            while i >= 0 > List.isEmpty stack do
                match stack with
                | (_, x) :: xs ->
                    arr.[i] <- x
                    i <- i - 1
                    stack <- xs
                | [] -> ()
            arr
        interface IDisposable with
            member _.Dispose() = ArrayPool.Shared.Return(arr, true)

    /// <summary>Parses and post-processes tokens with the LALR(1) algorithm.</summary>
    /// <param name="lalrStates">The LALR state table to use.</param>
    /// <param name="oops">The <see cref="OptimizedOperations"/> object that will make the parsing faster.</param>
    /// <param name="pp">The <see cref="PostProcessor"/> to use on the newly-fused productions.</param>
    /// <param name="fToken">A <see cref="Tokenizer"/> object to get the next tokens.</param>
    /// <param name="input">The <see cref="InputStream"/>.</param>
    /// <exception cref="ParseError">An error did happen. In Farkle,
    /// this exception is caught by the <see cref="RuntimeFarkle"/></exception>
    let parseLALR (lalrStates: ImmutableArray<LALRState>) (pp: PostProcessor<'TResult>) (tokenizer: Tokenizer) (input: CharStream) =
        use objBuffer = new ObjectBuffer()
        let oops = tokenizer.OptimizedOperations
        let fail msg: obj = (input.LastTokenPosition, msg) |> Message |> ParserException |> raise
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
                    let nextToken = tokenizer.GetNextToken(pp, input)
                    // We can't use <| because it prevents optimization into a loop.
                    // See https://github.com/dotnet/fsharp/issues/6984 for details.
                    impl nextToken nextState ((nextState, data) :: stack)
                | None -> failwithf "Error in state %d: the parser cannot emit shift when EOF is encountered." currentState.Index
            | Some(LALRAction.Reduce productionToReduce) ->
                let handleLength = productionToReduce.Handle.Length
                let stack' = List.skip handleLength stack
                /// The stack cannot be empty; we gave it one element in the beginning.
                let nextState = fst stack'.Head
                match oops.LALRGoto productionToReduce.Head nextState with
                | Some nextState ->
                    let resultObj =
                        let tokens = objBuffer.GetBufferFromStack handleLength stack
                        pp.Fuse(productionToReduce, tokens)
                    impl token nextState ((nextState, resultObj) :: stack')
                | None -> failwithf "Error in state %d: GOTO was not found for production %O." nextState.Index productionToReduce
            | None ->
                let expectedSymbols =
                    currentState.Actions.Keys
                    |> Seq.map ExpectedSymbol.Terminal
                    |> set
                    |> (fun s -> if currentState.EOFAction.IsSome then Set.add ExpectedSymbol.EndOfInput s else s)
                let foundToken =
                    match token with
                    | Some {Symbol = term} -> ExpectedSymbol.Terminal term
                    | None -> ExpectedSymbol.EndOfInput
                (expectedSymbols, foundToken) |> ParseErrorType.SyntaxError |> fail
        let firstToken = tokenizer.GetNextToken(pp, input)
        impl firstToken lalrStates.[0] [lalrStates.[0], null] :?> 'TResult
