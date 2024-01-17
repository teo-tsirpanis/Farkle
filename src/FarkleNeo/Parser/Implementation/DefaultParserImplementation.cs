// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Collections;
using Farkle.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;
using System.Collections.Immutable;
using System.Diagnostics;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Farkle.Parser.Implementation;

internal readonly struct DefaultParserImplementation<TChar>
{
    public Grammar Grammar { get; }
    private readonly LrStateMachine _lrStateMachine;
    private readonly object _semanticProvider;
    public Tokenizer<TChar> Tokenizer { get; }

    private const int InitialStackCapacity = 64;

    private ITokenSemanticProvider<TChar> TokenSemanticProvider => Utilities.UnsafeCast<ITokenSemanticProvider<TChar>>(_semanticProvider);
    private IProductionSemanticProvider ProductionSemanticProvider => Utilities.UnsafeCast<IProductionSemanticProvider>(_semanticProvider);

    private DefaultParserImplementation(Grammar grammar, LrStateMachine lrStateMachine, object semanticProvider, Tokenizer<TChar> tokenizer)
    {
        Grammar = grammar;
        _lrStateMachine = lrStateMachine;
        _lrStateMachine.PrepareForParsing();
        _semanticProvider = semanticProvider;
        Tokenizer = tokenizer;
    }

    public static DefaultParserImplementation<TChar> Create<T>(Grammar grammar, LrStateMachine lrStateMachine, ISemanticProvider<TChar, T> semanticProvider, Tokenizer<TChar> tokenizer)
    {
        Debug.Assert(!lrStateMachine.HasConflicts);
        return new(grammar, lrStateMachine, semanticProvider, tokenizer);
    }

    public DefaultParserImplementation<TChar> WithTokenizer(Tokenizer<TChar> tokenizer) =>
        new(Grammar, _lrStateMachine, _semanticProvider, tokenizer);

    public DefaultParserImplementation<TChar> WithSemanticProvider<T>(ISemanticProvider<TChar, T> semanticProvider) =>
        new(Grammar, _lrStateMachine, semanticProvider, Tokenizer);

    private int Reduce(ref ParserInputReader<TChar> input, in GrammarTablesHotData hotData,
        ref ValueStack<int> stateStack, ref ValueStack<object?> semanticValueStack, ProductionHandle production)
    {
        int membersLength = hotData.GetProductionMemberCount(production);
        int goFromState = stateStack.Peek(membersLength);
        int gotoState = _lrStateMachine.GetGoto(goFromState, hotData.GetProductionHead(production));
        object? semanticValue = ProductionSemanticProvider.Fuse(ref input.State, production, semanticValueStack.PeekMany(membersLength));
        semanticValueStack.PopMany(membersLength);
        semanticValueStack.Push(semanticValue);
        stateStack.PopMany(membersLength);
        stateStack.Push(gotoState);
        return gotoState;
    }

    private RunResult Run(ref ParserInputReader<TChar> input, ref ValueStack<int> stateStack, ref ValueStack<object?> semanticValueStack, out object? result)
    {
        GrammarTablesHotData hotData = new(Grammar);
        int currentState = stateStack.Peek();
        bool foundToken = Tokenizer.TryGetNextToken(ref input, TokenSemanticProvider, out TokenizerResult token);
        while (true)
        {
            if (!foundToken)
            {
                if (!input.IsFinalBlock)
                {
                    result = null;
                    return RunResult.NeedsMoreInput;
                }
            RetryEof:
                LrEndOfFileAction eofAction = _lrStateMachine.GetEndOfFileAction(currentState);
                if (eofAction.IsAccept)
                {
                    result = semanticValueStack.Peek();
                    return RunResult.Success;
                }
                if (eofAction.IsReduce)
                {
                    currentState = Reduce(ref input, in hotData, ref stateStack, ref semanticValueStack, eofAction.ReduceProduction);
                    goto RetryEof;
                }
            }
            else if (!token.IsSuccess)
            {
                result = ParserUtilities.SupplyParserStateInfo(token.Data,
                    ParserUtilities.GetExpectedSymbols(Grammar, _lrStateMachine[currentState]),
                    currentState);
                return RunResult.Failure;
            }
            else
            {
            RetryToken:
                LrAction action = _lrStateMachine.GetAction(currentState, token.Symbol);
                if (action.IsShift)
                {
                    currentState = action.ShiftState;
                    stateStack.Push(currentState);
                    semanticValueStack.Push(token.Data);
                    foundToken = Tokenizer.TryGetNextToken(ref input, TokenSemanticProvider, out token);
                    continue;
                }
                if (action.IsReduce)
                {
                    currentState = Reduce(ref input, in hotData, ref stateStack, ref semanticValueStack, action.ReduceProduction);
                    goto RetryToken;
                }
                TokenSymbolAttributes flags = hotData.GetTokenSymbolFlags(token.Symbol);
                // TODO: Add a test once we add the builder and can define noise terminals.
                if ((flags & TokenSymbolAttributes.Noise) != 0)
                {
                    foundToken = Tokenizer.TryGetNextToken(ref input, TokenSemanticProvider, out token);
                    continue;
                }
            }
            TextPosition errorPos = foundToken ? token.Position : input.State.CurrentPosition;
            string? actualTokenName = foundToken ? Grammar.GetString(Grammar.GetTokenSymbol(token.Symbol).Name) : null;
            ImmutableArray<string?> expectedTokens = ParserUtilities.GetExpectedSymbols(Grammar, _lrStateMachine[currentState]);
            result = new ParserDiagnostic(errorPos, new SyntaxError(actualTokenName, expectedTokens, currentState));
            return RunResult.Failure;
        }
    }

    private unsafe void RunOneShot<T>(ref ParserInputReader<TChar> input, ref ParserCompletionState<T> completionState)
    {
        ValueStack<int> stateStack = new(stackalloc int[InitialStackCapacity]);
#if NET8_0_OR_GREATER
        ObjectBuffer semanticValueBuffer = default;
        ValueStack<object?> semanticValueStack = new(semanticValueBuffer);
#else
        ValueStack<object?> semanticValueStack = new(InitialStackCapacity);
#endif
        stateStack.Push(_lrStateMachine.InitialState);
        semanticValueStack.Push(null);
#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        RunResult runResult = Run(ref input, ref stateStack, ref semanticValueStack, out object? result);
#pragma warning restore CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        switch (runResult)
        {
            case RunResult.Success:
                completionState.SetSuccess((T)result!);
                break;
            case RunResult.Failure:
                Debug.Assert(result is not null);
                completionState.SetError(result);
                break;
        }
        stateStack.Dispose();
        semanticValueStack.Dispose();
    }

    public void Run<T>(ref ParserInputReader<TChar> input, ref ParserCompletionState<T> completionState)
    {
        if (input.IsFinalBlock && !input.State.TryGetValue(typeof(State), out _))
        {
            RunOneShot(ref input, ref completionState);
            return;
        }
        State state = State.GetOrCreate(_lrStateMachine, ref input.State);
        var stateStack = new ValueStack<int>(state.StateStack);
        var semanticValueStack = new ValueStack<object?>(state.SemanticValueStack);
        RunResult result = Run(ref input, ref stateStack, ref semanticValueStack, out object? runResult);
        switch (result)
        {
            case RunResult.Success:
                completionState.SetSuccess((T)runResult!);
                break;
            case RunResult.Failure:
                Debug.Assert(runResult is not null);
                completionState.SetError(runResult);
                break;
        }
        if (result == RunResult.NeedsMoreInput)
        {
            state.StateStack = stateStack.ExportState();
            state.SemanticValueStack = semanticValueStack.ExportState();
        }
        else
        {
            stateStack.Dispose();
            semanticValueStack.Dispose();
        }
    }

    private enum RunResult
    {
        Success,
        Failure,
        NeedsMoreInput
    }

    private sealed class State
    {
        public ValueStack<int>.State StateStack;
        public ValueStack<object?>.State SemanticValueStack;

        public static State GetOrCreate(LrStateMachine lrStateMachine, ref ParserState parserState)
        {
            if (!parserState.TryGetValue(typeof(State), out object? state))
            {
                state = new State
                {
                    StateStack = CreateStack(lrStateMachine.InitialState),
                    SemanticValueStack = CreateStack<object?>(null)
                };
                parserState.SetValue(typeof(State), state);
            }
            return (State)state;

            static ValueStack<T>.State CreateStack<T>(T initialValue)
            {
                var stack = new ValueStack<T>(InitialStackCapacity);
                stack.Push(initialValue);
                return stack.ExportState();
            }
        }
    }

#if NET8_0_OR_GREATER
    [InlineArray(InitialStackCapacity)]
    private struct ObjectBuffer
    {
        private object? _x;
    }
#endif
}
