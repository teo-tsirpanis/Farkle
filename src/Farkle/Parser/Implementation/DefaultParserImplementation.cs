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
using System.Runtime.CompilerServices;

using static Farkle.Parser.Implementation.DefaultParserImplementation;

namespace Farkle.Parser.Implementation;

internal readonly struct DefaultParserImplementation<TChar>
{
    public Grammar Grammar { get; }
    private readonly LrWithoutConflicts _lrStateMachine;

    public ISemanticProvider<TChar> SemanticProvider { get; init; }
    public Tokenizer<TChar> Tokenizer { get; init; }
    public ParserOptions Options { get; init; } = new();

    public DefaultParserImplementation(Grammar grammar, LrWithoutConflicts lrStateMachine, ISemanticProvider<TChar> semanticProvider, Tokenizer<TChar> tokenizer)
    {
        Grammar = grammar;
        _lrStateMachine = lrStateMachine;
        _lrStateMachine.PrepareForParsing();
        SemanticProvider = semanticProvider;
        Tokenizer = tokenizer;
    }

    private int Reduce(ref ParserInputReader<TChar> input, in GrammarTablesHotData hotData,
        ref ValueStack<int> stateStack, ref ValueStack<object?> semanticValueStack, ProductionHandle production)
    {
        int membersLength = hotData.GetProductionMemberCount(production);
        int goFromState = stateStack.Peek(membersLength);
        int gotoState = _lrStateMachine.GetGoto(goFromState, hotData.GetProductionHead(production));
        object? semanticValue = SemanticProvider.Fuse(ref input.State, production, semanticValueStack.PeekMany(membersLength));
        semanticValueStack.PopMany(membersLength);
        semanticValueStack.Push(semanticValue);
        stateStack.PopMany(membersLength);
        stateStack.Push(gotoState);
        return gotoState;
    }

    /// <summary>
    /// Checks whether the parser will accept the given symbol after performing one or more reductions.
    /// No semantic actions are executed, and the parser's state stack remains unchanged after this function returns.
    /// </summary>
    /// <param name="hotData">The grammar's <see cref="GrammarTablesHotData"/>.</param>
    /// <param name="stateStack">The parser's state stack.</param>
    /// <param name="symbol">The symbol that caused the reduction.</param>
    /// <param name="reduceProduction">The first production to reduce.</param>
    private bool TrySpeculativeReduce(in GrammarTablesHotData hotData, ref ValueStack<int> stateStack, TokenSymbolHandle symbol, ProductionHandle reduceProduction)
    {
        // The current state we are at. Instead of pushing it to the stack and peeking, we keep track of it separately.
        // This way, we actually push only when we reduce an empty production.
        int currentState = stateStack.Peek();
        // The number of items that were on the stack in the beginning. We must not pop beyond this point.
        int originalStackCount = stateStack.Count;
        // The number of items that we have non-destructively popped beyond the original stack count.
        int virtualPoppedCount = 0;
        LrAction action;
        while (true)
        {
            int membersLength = hotData.GetProductionMemberCount(reduceProduction);
            if (membersLength == 0)
            {
                int gotoState = _lrStateMachine.GetGoto(currentState, hotData.GetProductionHead(reduceProduction));
                stateStack.Push(gotoState);
                currentState = gotoState;
            }
            else
            {
                int extraPushedCount = stateStack.Count - originalStackCount;
                int goFromStateIndex;
                if (membersLength <= extraPushedCount)
                {
                    goFromStateIndex = membersLength;
                }
                else
                {
                    goFromStateIndex = virtualPoppedCount + membersLength;
                }
                int goFromState = stateStack.Peek(goFromStateIndex);
                int gotoState = _lrStateMachine.GetGoto(goFromState, hotData.GetProductionHead(reduceProduction));
                currentState = gotoState;
                int statesToPop = membersLength - 1;
                // First pop from the extra pushed states, then virtually pop what was left.
                if (statesToPop >= extraPushedCount)
                {
                    stateStack.PopMany(extraPushedCount);
                    statesToPop -= extraPushedCount;
                }
                virtualPoppedCount += statesToPop;
            }
            action = _lrStateMachine.GetAction(currentState, symbol);
            if (!action.IsReduce)
            {
                break;
            }
            reduceProduction = action.ReduceProduction;
        }
        Debug.Assert(stateStack.Count >= originalStackCount);
        // Restore the stack to its original state.
        if (stateStack.Count > originalStackCount)
        {
            stateStack.PopMany(stateStack.Count - originalStackCount);
        }
        return action.IsShift;
    }

    private ImmutableArray<string?> GetExpectedSymbols(in GrammarTablesHotData hotData, ref ValueStack<int> stateStack)
    {
        var builder = ImmutableArray.CreateBuilder<string?>();
        int state = stateStack.Peek();
        foreach (var action in _lrStateMachine[state].Actions)
        {
            if ((action.Key.Attributes & TokenSymbolAttributes.Hidden) != 0)
            {
                continue;
            }
            if (action.Value.IsError)
            {
                continue;
            }
            if (action.Value.IsShift)
            {
                builder.Add(action.Key.Name);
            }
            else if (action.Value.IsReduce && (Options.LookaheadCorrection == LookaheadCorrectionOption.Never || TrySpeculativeReduce(in hotData, ref stateStack, action.Key.Handle, action.Value.ReduceProduction)))
            {
                builder.Add(action.Key.Name);
            }
        }
        if (_lrStateMachine[state].EndOfFileActions.Count > 0)
        {
            builder.Add(null);
        }
        return builder.DrainToImmutable();
    }

    private object SupplyParserStateInfo(object diagnostic, in GrammarTablesHotData hotData, ref ValueStack<int> stateStack)
    {
        return diagnostic switch
        {
            IParserStateInfoSupplier x => x.WithParserStateInfo(GetExpectedSymbols(in hotData, ref stateStack), stateStack.Peek()),
            ParserDiagnostic { Message: IParserStateInfoSupplier x, Position: var position } =>
                new ParserDiagnostic(position, x.WithParserStateInfo(GetExpectedSymbols(in hotData, ref stateStack), stateStack.Peek())),
            _ => diagnostic
        };
    }

    private RunResult Run(ref ParserInputReader<TChar> input, ref ValueStack<int> stateStack, ref ValueStack<object?> semanticValueStack, out object? result)
    {
        GrammarTablesHotData hotData = new(Grammar);
        int currentState = stateStack.Peek();
        while (true)
        {
            bool foundToken = Tokenizer.TryGetNextToken(ref input, SemanticProvider, out TokenizerResult token);
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
                result = SupplyParserStateInfo(token.Data, in hotData, ref stateStack);
                return RunResult.Failure;
            }
            else
            {
                bool performLac = Options.LookaheadCorrection == LookaheadCorrectionOption.Always;
            RetryToken:
                LrAction action = _lrStateMachine.GetAction(currentState, token.Symbol);
                if (action.IsShift)
                {
                    currentState = action.ShiftState;
                    stateStack.Push(currentState);
                    semanticValueStack.Push(token.Data);
                    continue;
                }
                if (action.IsReduce && (!performLac || TrySpeculativeReduce(in hotData, ref stateStack, token.Symbol, action.ReduceProduction)))
                {
                    // Perform LAC only once per token.
                    performLac = false;
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
                if ((flags & TokenSymbolAttributes.Noise) != 0)
                {
                    continue;
                }
            }
            TextPosition errorPos = foundToken ? token.Position : input.State.CurrentPosition;
            string? actualTokenName = foundToken ? Grammar.GetTokenSymbol(token.Symbol).Name : null;
            ImmutableArray<string?> expectedTokens = GetExpectedSymbols(in hotData, ref stateStack);
            result = new ParserDiagnostic(errorPos, new SyntaxError(actualTokenName, expectedTokens, currentState));
            return RunResult.Failure;
        }
    }

    private unsafe RunResult RunOneShot(ref ParserInputReader<TChar> input, out object? resultValue)
    {
        ValueStack<int> stateStack = new(stackalloc int[InitialStackCapacity]);
        ObjectBuffer semanticValueBuffer = default;
        ValueStack<object?> semanticValueStack = new(semanticValueBuffer);

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

    [InlineArray(InitialStackCapacity)]
    public struct ObjectBuffer
    {
        private object? _x;
    }

    public enum RunResult
    {
        Success,
        Failure,
        NeedsMoreInput
    }
}
