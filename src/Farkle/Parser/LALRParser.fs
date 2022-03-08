// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Collections
open Farkle.Common
open Farkle.Grammars
open Farkle.IO
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// Functions to syntactically parse a series of tokens using the LALR algorithm.
module internal LALRParser =

    let rec private parse_impl (lalrStates: ImmutableArray<_>) (oops: OptimizedOperations) (tokenizer: Tokenizer) (pp: IPostProcessor)
        (input: CharStream) (objectStack: obj StackNeo byref) (stateStack: uint32 StackNeo byref) (token: Token) currentState =
        let (|LALRState|) x = lalrStates.[int x]
        let nextAction =
            if not token.IsEOF then
                oops.GetLALRAction token.Symbol currentState
            else
                currentState.EOFAction
        match nextAction with
        | Some LALRAction.Accept -> objectStack.Peek()
        | Some(LALRAction.Shift (LALRState nextState)) ->
            let nextToken = tokenizer.GetNextToken(pp, input)
            objectStack.Push token.Data
            stateStack.Push nextState.Index
            parse_impl lalrStates oops tokenizer pp input &objectStack &stateStack nextToken nextState
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
                parse_impl lalrStates oops tokenizer pp input &objectStack &stateStack token gotoState
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

    [<Literal>]
    let private initialStackCapacity =
#if DEBUG
        1
#else
        64
#endif

    /// <summary>Parses and post-processes tokens with the LALR(1) algorithm.</summary>
    /// <param name="grammar">The grammar to use.</param>
    /// <param name="pp">The <see cref="IPostProcessor"/> to use on the newly-fused productions.</param>
    /// <param name="tokenizer">A <see cref="Tokenizer"/> object that gives the next tokens.</param>
    /// <param name="input">The <see cref="CharStream"/> whose characters are to be parsed.</param>
    /// <exception cref="FarkleException">An error did happen. Apart from <see cref="PostProcessorException"/>,
    /// subclasses of this exception class are caught by the runtime Farkle API.</exception>
#if NET
    // Avoid zero-inizitalizing the initial state stack buffer.
    [<SkipLocalsInit>]
#endif
    let parse grammar (pp: IPostProcessor) (tokenizer: Tokenizer) (input: CharStream) =
        // This method will allocate a lot on the stack.
        RuntimeHelpers.EnsureSufficientExecutionStack()

#if MODERN_FRAMEWORK
        let mutable sixtyFourObjects = Unchecked.defaultof<Stack.SixtyFour<obj>>
        let mutable objectStack = StackNeo(Stack.createSixtyFourSpan &sixtyFourObjects)
#else
        let mutable objectStack = StackNeo(initialStackCapacity)
#endif
        objectStack.Push null

        let mutable stateStack = StackNeo(Stack.allocSpan initialStackCapacity)
        stateStack.Push 0u

        try
            let oops = OptimizedOperations.Create grammar
            let lalrStates = grammar.LALRStates

            let firstToken = tokenizer.GetNextToken(pp, input)
            parse_impl lalrStates oops tokenizer pp input &objectStack &stateStack firstToken lalrStates.[0]
        finally
            stateStack.Dispose()
            objectStack.Dispose()
