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

/// Functions to syntactically parse a series of tokens using the LALR algorithm.
module LALRParser =

    type private ObjectBuffer() =
        let mutable arr = ArrayPool.Shared.Rent 16
        let ensureSized newSize =
            if newSize > arr.Length then
                ArrayPool.Shared.Return arr
                arr <- ArrayPool.Shared.Rent newSize
        member _.GetBufferFromStack(length, stack) =
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
            ReadOnlySpan(arr, 0, length)
        interface IDisposable with
            member _.Dispose() = ArrayPool.Shared.Return(arr, true)

    /// <summary>Parses and post-processes tokens with the LALR(1) algorithm.</summary>
    /// <param name="grammar">The grammar to use.</param>
    /// <param name="pp">The <see cref="PostProcessor"/> to use on the newly-fused productions.</param>
    /// <param name="tokenizer">A <see cref="Tokenizer"/> object that gives the next tokens.</param>
    /// <param name="input">The <see cref="CharStream"/> whose characters are to be parsed.</param>
    /// <exception cref="FarkleException">An error did happen. Apart from <see cref="PostProcessorException"/>,
    /// subclasses of this exception class are caught by the runtime Farkle API.</exception>
    let parse grammar (pp: PostProcessor<'TResult>) (tokenizer: Tokenizer) (input: CharStream) =
        use objBuffer = new ObjectBuffer()
        let oops = OptimizedOperations.Create grammar
        let lalrStates = grammar.LALRStates

        let rec impl (token: Token) currentState stack =
            let (|LALRState|) x = lalrStates.[int x]
            let nextAction =
                if not token.IsEOF then
                    oops.GetLALRAction token.Symbol currentState
                else
                    currentState.EOFAction
            match nextAction with
            | Some LALRAction.Accept -> stack |> List.head |> snd
            | Some(LALRAction.Shift (LALRState nextState)) ->
                if token.IsEOF then
                    failwithf "Error in state %d: the parser cannot emit shift when EOF is encountered." currentState.Index
                let nextToken = tokenizer.GetNextToken(pp, input)
                impl nextToken nextState ((nextState, token.Data) :: stack)
            | Some(LALRAction.Reduce productionToReduce) ->
                let handleLength = productionToReduce.Handle.Length
                let newStack = List.skip handleLength stack
                // The stack cannot be empty; we gave it one element in the beginning.
                let gofromState, _ = newStack.Head
                match oops.LALRGoto productionToReduce.Head gofromState with
                | Some gotoState ->
                    let resultObj =
                        let tokens = objBuffer.GetBufferFromStack(handleLength, stack)
                        try
                            pp.Fuse(productionToReduce, tokens)
                        with
                        | :? ParserApplicationException -> reraise()
                        | e -> PostProcessorException(productionToReduce, e) |> raise
                    impl token gotoState ((gotoState, resultObj) :: newStack)
                | None -> failwithf "Error in state %d: GOTO was not found for production %O." gofromState.Index productionToReduce
            | None ->
                let expectedSymbols =
                    currentState.Actions.Keys
                    |> Seq.map ExpectedSymbol.Terminal
                    |> set
                    |> (fun s -> if currentState.EOFAction.IsSome then Set.add ExpectedSymbol.EndOfInput s else s)
                let foundToken =
                    if not token.IsEOF then
                        ExpectedSymbol.Terminal token.Symbol
                    else
                        ExpectedSymbol.EndOfInput
                let syntaxError = ParseErrorType.SyntaxError(expectedSymbols, foundToken)
                ParserError(token.Position, syntaxError) |> ParserException |> raise

        let firstToken = tokenizer.GetNextToken(pp, input)
        impl firstToken lalrStates.[0] [lalrStates.[0], null] :?> 'TResult
