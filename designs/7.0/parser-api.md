# Farkle 7's parser API

One of Farkle's distinguishing features is that it's _dead-simple_ to parse stuff. Since Farkle 3 there is a class called `RuntimeFarkle<TResult>` and once you get an instance of it, you can `Parse` a string, a `ReadOnlyMemory<char>`, a `TextReader` or a file path and that's it. You get a `TResult` if parsing succeeded or an error object if it did not. The entire lexical, syntax and semantic analysis is encapsulated in a single object and performed with a single method call[^antlr]. We need to keep this spirit for Farkle 7, but there is a lot of room for improvement.

## Requirements

Requirements with a 🆕 are new for Farkle 7.

* __Provide a very simple API surface for the most common use cases.__
* 🆕 __Provide an abstraction layer over Farkle's parsers that can be implemented by 3rd-party code.__ Farkle supports LR parsers, one would theoretically be able to implement a different parser algorithm on top of Farkle's abstractions.
    * The abstractions and their default implementation will still be in the same package. I am very reluctant to split the Farkle library to multiple packages.
* 🆕 __Support extending parser objects with a wide variety of possible features.__
* __Support customizing every part of the parser's pipeline.__ In Farkle 6 you can customize the input reader, the tokenizer, and the semantic analyzer. The parser itself can be customized, in the sense that you can take the rest of the components apart and use them on their own.
    * 🆕 __And make it easier.__
* __Support parsing text from either a contiguous memory region in a zero-copy fashion or a streaming input.__
    * 🆕 __And `ReadOnlySpan<char>`.__
* 🆕 __Support parsing streaming input asynchronously.__
* 🆕 __Pave the way to support various character types.__ Besides the good old 16-bit `char`, the same API must easily enable writing parsers for raw `byte`s, which will enable supporting UTF-8 parsers. The initial Farkle 7.0 release will only provide the abstractions to make it possible; a future version might actually add support for UTF-8 in the parser and the builder.
* 🆕 __Eliminate all memory allocations from Farkle's default tokenizer and parser.__ Other components like the semantic analyzer or the buffers for the streaming input may still allocate memory, but if you are syntax-checking a string, it must be (at least amortized) allocation-free. This requirement will be guaranteed only when using at least the latest framework Farkle targets.
* 🆕 __Use standard terminology in the API's namings.__ Types like `RuntimeFarkle` and `PostProcessor` will be renamed.
* 🆕 __Remove dependency on F#'s `Result` type.__

## The idea

Farkle 7's parser API will employ a _push-based_ model. Previous versions of Farkle are _pull-based_, in the sense that the parser[^parser] "pulls" characters from the input stream when the buffer gets filled up. Farkle 7 flips this model on its head. The parser itself does not read the characters. The calling code does read the characters and tells the parser to process them. Once the parser finishes with them, it exits, we read the next characters and invoke the parser again. If input ends, we tell the parser, so that next time we invoke it, it injects an EOF token. If we want to parse a span of characters, we invoke the parser once with that span as the input.

This model allows us to suspend the parser between invocations, and use any means to read the characters. Reading them with an `await textReader.ReadAsync()` becomes surprisingly simple, compared to a pull-based model, where the entire parsing pipeline would have to support async, penalizing all non-async use cases.

## The code

Let's get into the code. The following API shapes are a general idea and not final. Additional members might be proposed in separate design documents.

### State management

We start by defining a type to keep the state of a parsing operation and provide a read-only view of it to external code. It will be passed to transformers by reference and replace the existing `Farkle.ITransformerContext` interface.

```csharp
namespace Farkle.Parser;

public struct ParserState
{
    public readonly Position CurrentPosition { get; }
    public readonly long TotalCharactersRead { get; }

    public object? Context { readonly get; init; }

    // Extensibility points for user code.
    public void SetValue(object key, object value);
    public readonly bool TryGetValue(object key, [MaybeNullWhen(false)] out object value);
    public bool RemoveValue(object key);
}
```

All state of a parser will be held in the `ParserState`. All other parser objects (tokenizers, semantic providers) will be stateless and singletons. This will both simplify things and improve performance; no more creating a custom tokenizer every time we parse something.

`ParserState` has only one position property, `CurrentPosition`. It is equivalent to `ITransformerContext.StartPosition` in Farkle 6. A replacement for `ITransformerContext.EndPosition` will not be provided. This will simplify some things around position tracking and updating these two values.

The proposed extensibility points are superior to the existing `ITransformerContext.ObjectStore` property. Different components of a parser cannot conflict because the keys are arbitrary objects, and there is no API to clear or enumerate all values. If you have a `private static readonly object MyKey = new();` and you use it as a key, you can be sure that no other component will ever touch it. `ObjectStore` could be shimmed on top of these objects if there is a demand for it.

To avoid the allocation overhead of the extensibility points, the `Context` property can point to one arbitrary object and is held directly into the state. It is intended to be used only by the main parser code; for user extensions this object will not make sense.

### Reading input

A `ParserState` can be manipulated with a `ParserInputReader`, the replacement of Farkle 6's `CharStream`. It contains a reference to a `ParserState` and a read-only span of characters. The type of characters is generic to enable supporting byte parsers in the future.

```csharp
namespace Farkle.Parser;

public ref struct ParserInputReader<TChar>
{
#if NET7_0_OR_GREATER
    // Takes advantage of .NET 7's ref fields. Could be exposed in .NET Standard 2.1+ but it is
    // not certain that the ref safety rules would be enforced if an older SDK is being used.
    // Still we can internally make use of it.
    public ParserInputReader(ref ParserState state, ReadOnlySpan<TChar> input, bool isFinal = true);
#endif

    // This is our only choice for the frameworks that do not support ref fields.
    public ParserInputReader(IParserStateBox stateBox, ReadOnlySpan<TChar> input, bool isFinal = true);

    public readonly ReadOnlySpan<TChar> RemainingCharacters { get; }

    // Whether this is the final block of input.
    public readonly bool IsFinalBlock { get; }

    public ref ParserState State { get; }

    public void AdvanceBy(int count);
}

public interface IParserStateBox
{
    ref ParserState State { get; }
}

#if NET7_0_OR_GREATER
[Obsolete("Use a ParserState and pass it to the ParserInputReader's constructor by reference instead.")]
#endif
public sealed class ParserStateBox : IParserStateBox
{
    public ParserStateBox();

    public ref ParserState State { get; }
}
```

In frameworks that do not support ref fields, we have to use an interface and put the `ParserState` on the heap. A `ParserStateBox` class is provided for convenience and will be marked as obsolete on .NET 7.0+ to encourage the more efficient alternative.

The `AdvanceBy` method will update the `TokenStartPosition` and `TotalCharactersRead` properties of `ParserState`. If the character type is `char` or `byte`, the column will be updated according to any CR or LF characters encountered. Otherwise only the row number will be updated.

### The parser

And now we get to the parser itself. We first have to define a type to represent the result of a parsing operation. It is a discriminated union with two cases: success and error. The success value is generic, while the error value is an `object`, for simplicity.

```csharp
namespace Farkle.Parser;

public readonly struct ParserResult<T>
{
    public bool IsSuccess { get; }
    public bool IsError { get; }

    // Will throw if IsSuccess is false.
    public T Value { get; }
    // Will throw if IsError is false.
    public object Error { get; }
}

public static class ParserResult
{
    public static Result<T> CreateSuccess<T>(T value);
    public static Result<T> CreateError<T>(object error);
}

// Holds whether the parser has completed, and its result.
// We can't put that inside ParserState because the result type can
// be anything and we can neither make it generic or box the result.
public struct ParserCompletionState<T>
{
    public readonly bool IsCompleted { get; }
    // Will throw if IsCompleted is false.
    public readonly ParserResult<T> Result { get; }

    // Will throw if IsCompleted is true.
    public void SetResult(ParserResult<T> result);
}

public static class ParserCompletionStateExtensions
{
    public static void SetSuccess<T>(this ref ParserCompletionState<T> state, T value);
    public static void SetError<T>(this ref ParserCompletionState<T> state, object error);
    // Used to easily implement the non-generic parser interface.
    public static void CopyTo<T>(this in ParserCompletionState<T> state, ref ParserCompletionState<object> other);
}

public interface IParser<TChar> : IServiceProvider
{
    void Run(ref ParserInputReader<TChar> inputReader, ref ParserCompletionState<object?> completionState);
}

public interface IParser<TChar, T> : IParser<TChar>
{
    // A default implementation of the non-generic IParser interface will be provided on supported frameworks.
    void IParser<TChar>.Run(ref ParserInputReader<TChar> inputReader, ref ParserCompletionState<object?> completionState) => …;
    void Run(ref ParserInputReader<TChar> inputReader, ref ParserCompletionState<T> completionState);
}
```

The `IParser.Run` methods encapsulate the parser logic. They take a reference to a `ParserInputReader` to read and advance the input characters and persist any state, and a reference to a `ParserCompletionState` to signal that a parsing operation has completed. If `Run` returns without setting the completion state, it means that the parser needs more characters. The amount of characters that must be kept in memory can be determined by comparing the `RemainingCharacters` property before and after the call to `Run`.

Parser objects also implement `IServiceProvider`, to allow them to expose arbitrary custom features. Default services will be described in a later section.

### Parsing streaming input

The `IParser` interfaces themselves support parsing streaming input but the responsibility to manage the input buffers falls to the user. To make this easier, Farkle provides the `ParserStateContext` classes that greatly simplify parsing streaming input.

```csharp
namespace Farkle.Parser;

public class ParserStateContextOptions
{
    public int InitialBufferSize { get; init; }
}

// The base of parser state contexts. Allows uniformly performing all operations but getting the result.
public abstract class ParserStateContext<TChar> : IParserStateBox, IBufferWriter<TChar>
{
    // User code must inherit from ParserStateContext<TChar, T> for some T instead.
    private protected ParserStateContext(ParserStateContextOptions? options = null);

    // Invariant: State.Context == this
    public ref ParserState State { get; }

    // These members are overridden by ParserStateContext<TChar, T>.
    // Whether the parsing operation has completed.
    public abstract bool IsCompleted { get; }
    // Performs a parsing step.
    public abstract void Run();
    // Resets the context's state to allow reusing it for another parsing operation.
    public virtual void Reset();

    // IBufferWriter<TChar> implementation.
    // Gets a span/memory to which new characters can be written.
    public Memory<TChar> GetMemory(int sizeHint = 0);
    public Span<TChar> GetSpan(int sizeHint = 0);
    // Signals that new characters have been written.
    public void Advance(int count);

    // Whether CompleteInput has been called.
    public bool IsInputCompleted { get; }
    // Signals that the input has ended.
    public void CompleteInput();
}

public abstract class ParserStateContext<TChar, T> : ParserStateContext<TChar>
{
    protected ParserStateContext(ParserStateContextOptions? options = null);

    // These members cannot be overridden by user code.
    public sealed override bool IsCompleted { get; }
    public sealed override void Run();
    public sealed override void Reset();

    // The result of the parsing operation. Will throw if IsCompleted is false.
    public ParserResult<T> Result { get; }

    // These members can be overridden by user code.
    // Performs any additional resetting logic.
    protected virtual void OnReset() {}
    protected abstract void Run(ref ParserInputReader<TChar> inputReader, ref ParserCompletionState<T?> completionState);
}

public static class ParserStateContext
{
    public static ParserStateContext<TChar, object?> Create<TChar>(IParser<TChar> parser, ParserStateContextOptions? options = null);
    public static ParserStateContext<TChar, T> Create<TChar, T>(IParser<TChar, T> parser, ParserStateContextOptions? options = null);
}
```

A `ParserStateContext` encapsulates a parsing operation on streaming input. It contains an `IParser<TChar,T>` that parses the text, a `ParserState` to keep track of the parser's state, a `ParserCompletionState<T>` to keep track if parsing has finished, a buffer to store the input characters, and the logic to make them all work together. It implements the `IBufferWriter<TChar>` interface to allow writing characters very efficiently and with zero copies.

Parsing streaming input with a `ParserStateContext` can be done like this:

```csharp
public static ParserResult<T> Parse<T>(IParser<char, T> parser, TextReader reader)
{
    ParserStateContext<char, T> context = ParserStateContext.Create(parser);
    while (!context.IsCompleted)
    {
        if (!context.IsInputCompleted)
        {
            ReadOnlySpan<char> buffer = context.GetSpan();
            int charsRead = reader.Read(buffer);
            if (charsRead == 0)
            {
                context.CompleteInput();
            }
            else
            {
                context.Advance(charsRead);
            }
        }
        context.Run();
    }
    return context.Result;
}
```

### Semantic analysis

Semantic analysis (called _post-processing_ in earlier versions of Farkle) is the process of converting a parse tree to an object meaningful for the application. This behavior is controlled by the `ISemanticProvider` interfaces.

```csharp
namespace Farkle.Parser.SemanticAnalysis;

public readonly struct SemanticValue
{
    public static SemanticValue CreateUnsafe<T>(T value);

    public T GetUnsafe<T>();
}

public interface ITransformer<TChar>
{
    SemanticValue Transform(ref ParserState state, TokenSymbolHandle terminal, ReadOnlySpan<TChar> data);
}

public interface IFuser
{
    SemanticValue Fuse(ref ParserState state, ProductionHandle production, Span<SemanticValue> children);
}

public interface ISemanticProvider<TChar, out T> : ITransformer<TChar>, IFuser {}
```

The following things changed since Farkle 6:

1. `PostProcessor` was renamed to `ISemanticProvider`. This adheres to the .NET naming conventions and makes the name more descriptive. The `Transform` and `Fuse` methods were not renamed.
2. The `Transform` and `Fuse` methods were split to two interfaces, `ITransformer<TChar>` and `IFuser`. This helps with separation of concerns and does not require having a character type to run the fuser. The `ISemanticProvider` interface is now a marker interface that combines the two, and adds a covariant generic parameter of the starting symbol's return type.
3. `Fuse` accepts a reference to a `ParserState`, allowing stateful fuses; why not?
4. `Fuse` accepts a read-write span of the production's member values, instead of a read-only span in previous versions of Farkle. This allows the span to be used as a temporary buffer by the fuser; it would easily enable certain scenarios and the buffer gets discarded afterwards either way.

Another difference is that in Farkle 7 the semantic providers return values of type `SemanticValue` instead of `object`. `SemanticValue` is a type that wraps an arbitrary value and can support storing  small value types without boxing, with the drawback that getting the value from a `SemanticValue` requires knowing a priori the value's type. Farkle's own semantic providers always know the type but using `SemanticValue` in general-purpose code needs caution; `SemanticValue.CreateUnsafe<string>("Hello").GetUnsafe<int>()` is undefined behavior and that's the reason for the `Unsafe` suffix in the method names.

### Predefined services

The initial release of Farkle 7 will provide the following parser services. All are optional.

#### Getting the grammar of a parser

The `IGrammarProvider` service interface allows getting the grammar of a parser, if the parser is backed by one (it doesn't have to). For simplicity the `Grammar` object also implements that interface.

```csharp
namespace Farkle.Grammars;

public interface IGrammarProvider
{
    Grammar GetGrammar();
}

public abstract class Grammar : IGrammarProvider
{
    public Grammar GetGrammar() => this;
}

public static class GrammarProviderExtensions
{
    public static Grammar? GetGrammar(this IServiceProvider serviceProvider);
}
```

#### Custom parser state contexts

To avoid allocating a parser state dictionary, the `ParserStateContext` class supports being subclassed, to store essential state in its descendants. The `IParserStateContextFactory` service interface provides an extensibility point for parsers to create their own state contexts. It will be used by the `ParserStateContext.Create` methods if available.

```csharp
namespace Farkle.Parser;

public interface IParserStateContextFactory<TChar>
{
    ParserStateContext<TChar, object?> Create(ParserStateContextOptions? options = null);
}

public interface IParserStateContextFactory<TChar, T> : IParserStateContextFactory<TChar>
{
    // A default implementation of the base interface will be provided on supported frameworks.
    ParserStateContext<TChar, object?> IParserStateContextFactory<TChar>.Create(ParserStateContextOptions? options = null) => …;
    ParserStateContext<TChar, T> Create(ParserStateContextOptions? options = null);
}
```

#### Intervening in the parser state machine

> **Warning**
> This service interface is not certain to ship in Farkle 7.0.0 due to unaddressed technical questions.

The usual way to advance the state of a parser is by feeding it with more characters. The characters will be processed by the tokenizer, the tokenizer will emit a token, and the LR parser will read the tokens and advance its state machine accordingly. We want to support bypassing the first stage and directly inject tokens into the parser's state machine. The `IParserStateMachineController` service interface allows doing that.

```csharp
namespace Farkle.Parser;

public interface IParserStateMachineController
{
    // The number of possible states in the parser's state machine.
    int StateCount { get; }
    // Returns an array of the names of the terminals expected by the parser in the given state.
    // A null value in the array indicates that the parser expects the end of input.
    ImmutableArray<string?> GetExpectedTerminals(int stateIndex);
    // Returns a handle to the grammar's symbol with the given special name.
    EntityHandle GetSymbolBySpecialName(string name, bool throwIfNotFound = false);

    // Returns whether the parser has been suspended in the middle of a token.
    // In that case injecting tokens is not allowed.
    bool IsTokenizing(ref ParserState state);
    // Returns the state the parser's state machine is in.
    int GetCurrentState(ref ParserState state);
    // Injects a token into the parser's state machine, performing the necessary
    // shift/reduce/goto actions.
    // Returns true if it succeeded, and false if either the parser is suspended in
    // the middle of a token, or the symbol is not expected in the current state.
    bool TryAddToken(ref ParserState state, EntityHandle symbol, SemanticValue value);
}
```

The first three methods provide general information about the parser's state machine and do not require a parsing operation. `GetSymbolBySpecialName` is equivalent to `Grammar.GetSymbolBySpecialName` but it does not require an entire grammar.

TODO: Write about the other three methods. There is a complication with `TryAddToken` on nonterminals: it will always perform a single goto from the current state; it will sometimes reject injections that intuitively make sense. For example in the grammar `<S> ::= <A> <B>; A ::= <>; <B> ::= "x"`, you cannot inject a `B` nonterminal if you are at the beginning.

TODO: What would happen if we inject a token in the middle of tokenizing? It might fail in some cases and doesn't make sense; we have to somehow block it.

[^antlr]: Contrast this with solutions like ANTLR [where you need six lines to parse a string and you are not even done yet](https://github.com/antlr/antlr4/blob/master/doc/csharp-target.md#how-do-i-use-the-runtime-from-my-project). Sure it's super flexible but the barrier of entry is super high. And creating a grammar has an even higher barrier.

[^parser]: By _parser_ here we mean the entire pipeline consisting of the tokenizer, the LR parser and the semantic analyzer.
