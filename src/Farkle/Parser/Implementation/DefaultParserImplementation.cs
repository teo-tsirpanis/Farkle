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

using static Farkle.Parser.Implementation.DefaultParserImplementation;

namespace Farkle.Parser.Implementation;

internal readonly struct DefaultParserImplementation<TChar>
{
    public Grammar Grammar { get; }
    private readonly LrWithoutConflicts _lrStateMachine;
    private readonly object _semanticProvider;
    public Tokenizer<TChar> Tokenizer { get; }

    private ITokenSemanticProvider<TChar> TokenSemanticProvider => Utilities.UnsafeCast<ITokenSemanticProvider<TChar>>(_semanticProvider);
    private IProductionSemanticProvider ProductionSemanticProvider => Utilities.UnsafeCast<IProductionSemanticProvider>(_semanticProvider);

    private DefaultParserImplementation(Grammar grammar, LrWithoutConflicts lrStateMachine, object semanticProvider, Tokenizer<TChar> tokenizer)
    {
        Grammar = grammar;
        _lrStateMachine = lrStateMachine;
        _lrStateMachine.PrepareForParsing();
        _semanticProvider = semanticProvider;
        Tokenizer = tokenizer;
    }

    public static DefaultParserImplementation<TChar> Create<T>(Grammar grammar, LrWithoutConflicts lrStateMachine, ISemanticProvider<TChar, T> semanticProvider, Tokenizer<TChar> tokenizer)
    {
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
                int reduceCount = 0;
            RetryEof:
                LrEndOfFileAction eofAction = _lrStateMachine.GetEndOfFileAction(currentState);
                if (eofAction.IsAccept)
                {
                    result = semanticValueStack.Peek();
                    return RunResult.Success;
                }
                if (eofAction.IsReduce)
                {
                    if (reduceCount++ > _lrStateMachine.Count)
                    {
                        // This should happen only if LR state machine in the grammar file is specially crafted to have a cycle.
                        // Throw an exception like in all invalid grammar errors; we don't have to gracefully fail with a named diagnostic.
                        ThrowHelpers.ThrowInvalidDataException("Encountered too many consecutive reductions; the grammar file might be malformed.");
                    }
                    try
                    {
                        currentState = Reduce(ref input, in hotData, ref stateStack, ref semanticValueStack, eofAction.ReduceProduction);
                    }
                    catch (ParserApplicationException ex)
                    {
                        result = ex.GetErrorObject(input.State.CurrentPosition);
                        return RunResult.Failure;
                    }
                    goto RetryEof;
                }
            }
            else if (!token.IsSuccess)
            {
                result = ParserUtilities.SupplyParserStateInfo(token.Data, _lrStateMachine[currentState]);
                return RunResult.Failure;
            }
            else
            {
                int reduceCount = 0;
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
                    if (reduceCount++ > _lrStateMachine.Count)
                    {
                        // This should happen only if LR state machine in the grammar file is specially crafted to have a cycle.
                        // Throw an exception like in all invalid grammar errors; we don't have to gracefully fail with a named diagnostic.
                        ThrowHelpers.ThrowInvalidDataException("Encountered too many consecutive reductions; the grammar file might be malformed.");
                    }
                    try
                    {
                        currentState = Reduce(ref input, in hotData, ref stateStack, ref semanticValueStack, action.ReduceProduction);
                    }
                    catch (ParserApplicationException ex)
                    {
                        result = ex.GetErrorObject(input.State.CurrentPosition);
                        return RunResult.Failure;
                    }
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
            string? actualTokenName = foundToken ? Grammar.GetTokenSymbol(token.Symbol).Name : null;
            ImmutableArray<string?> expectedTokens = ParserUtilities.GetExpectedSymbols(_lrStateMachine[currentState]);
            result = new ParserDiagnostic(errorPos, new SyntaxError(actualTokenName, expectedTokens, currentState));
            return RunResult.Failure;
        }
    }

    private unsafe RunResult RunOneShot(ref ParserInputReader<TChar> input, out object? resultValue)
    {
        ValueStack<int> stateStack = new(stackalloc int[InitialStackCapacity]);
#if NET8_0_OR_GREATER
        ObjectBuffer semanticValueBuffer = default;
        ValueStack<object?> semanticValueStack = new(semanticValueBuffer);
#else
        ValueStack<object?> semanticValueStack = new(InitialStackCapacity);
#endif
        stateStack.Push(_lrStateMachine.StartState);
        semanticValueStack.Push(null);
#pragma warning disable CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        RunResult runResult = Run(ref input, ref stateStack, ref semanticValueStack, out resultValue);
#pragma warning restore CS9080 // Use of variable in this context may expose referenced variables outside of their declaration scope
        stateStack.Dispose();
        semanticValueStack.Dispose();
        return runResult;
    }

    public RunResult Run(ref ParserInputReader<TChar> input, out object? resultValue)
    {
        if (input.IsFinalBlock && !input.State.TryGetValue(typeof(State), out _))
        {
            return RunOneShot(ref input, out resultValue);
        }
        State state = State.GetOrCreate(_lrStateMachine, ref input.State);
        var stateStack = new ValueStack<int>(state.StateStack);
        var semanticValueStack = new ValueStack<object?>(state.SemanticValueStack);
        RunResult runResult = Run(ref input, ref stateStack, ref semanticValueStack, out resultValue);
        if (runResult == RunResult.NeedsMoreInput)
        {
            state.StateStack = stateStack.ExportState();
            state.SemanticValueStack = semanticValueStack.ExportState();
        }
        else
        {
            stateStack.Dispose();
            semanticValueStack.Dispose();
        }
        return runResult;
    }
}

/// <summary>
/// Contains the parts of <see cref="DefaultParserImplementation{TChar}"/> that do
/// not depend on the character type.
/// </summary>
internal static class DefaultParserImplementation
{
    internal const int InitialStackCapacity = 64;

    public sealed class State
    {
        public ValueStack<int>.State StateStack;
        public ValueStack<object?>.State SemanticValueStack;

        public static State GetOrCreate(LrStateMachine lrStateMachine, ref ParserState parserState)
        {
            if (!parserState.TryGetValue(typeof(State), out object? state))
            {
                state = new State
                {
                    StateStack = CreateStack(lrStateMachine.StartState),
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
    public struct ObjectBuffer
    {
        private object? _x;
    }
#endif

    public enum RunResult
    {
        Success,
        Failure,
        NeedsMoreInput
    }
}
