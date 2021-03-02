// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.IO
open System.Runtime.CompilerServices

/// Functions to syntactically parse a series of tokens using the LALR algorithm.
module LALRParser =

    /// <summary>Parses and post-processes tokens with the LALR(1) algorithm.</summary>
    /// <param name="grammar">The grammar to use.</param>
    /// <param name="pp">The <see cref="PostProcessor"/> to use on the newly-fused productions.</param>
    /// <param name="tokenizer">A <see cref="Tokenizer"/> object that gives the next tokens.</param>
    /// <param name="input">The <see cref="CharStream"/> whose characters are to be parsed.</param>
    /// <exception cref="FarkleException">An error did happen. Apart from <see cref="PostProcessorException"/>,
    /// subclasses of this exception class are caught by the runtime Farkle API.</exception>
    let parse<[<Nullable(2uy)>] 'TResult> grammar (pp: PostProcessor<'TResult>) (tokenizer: Tokenizer) (input: CharStream): _ =
        let objectStack = StackNeo()
        objectStack.Push null
        let stateStack = StackNeo()
        stateStack.Push 0u
        let oops = OptimizedOperations.Create grammar
        let lalrStates = grammar.LALRStates

        let rec impl (token: Token) currentState =
            let (|LALRState|) x = lalrStates.[int x]
            let nextAction =
                if not token.IsEOF then
                    oops.GetLALRAction token.Symbol currentState
                else
                    currentState.EOFAction
            match nextAction with
            | Some LALRAction.Accept -> objectStack.Peek()
            | Some(LALRAction.Shift (LALRState nextState)) ->
                if token.IsEOF then
                    failwithf "Error in state %d: the parser cannot emit shift when EOF is encountered." currentState.Index
                let nextToken = tokenizer.GetNextToken(pp, input)
                objectStack.Push token.Data
                stateStack.Push nextState.Index
                impl nextToken nextState
            | Some(LALRAction.Reduce productionToReduce) ->
                let handleLength = productionToReduce.Handle.Length
                // The stack cannot be empty; we gave it one element in the beginning.
                let (LALRState gofromState) = stateStack.Peek handleLength
                match oops.LALRGoto productionToReduce.Head gofromState with
                | Some gotoState ->
                    let resultObj =
                        let members = objectStack.PeekMany handleLength
                        try
                            pp.Fuse(productionToReduce, members)
                        with
                        | :? ParserException
                        | :? ParserApplicationException -> reraise()
                        | e -> PostProcessorException(productionToReduce, e) |> raise
                    objectStack.PopMany handleLength
                    objectStack.Push resultObj
                    stateStack.PopMany handleLength
                    stateStack.Push gotoState.Index
                    impl token gotoState
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
        impl firstToken lalrStates.[0] :?> 'TResult
