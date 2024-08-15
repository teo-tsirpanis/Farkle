# Farkle 7's tokenizer API

Since version 6.0 Farkle supports customizing the logic of breaking the input text into a sequence of tokens, known as _tokenizing_. This is used to implement parsers for languages that cannot be tokenized with a simple DFA, such as indentation-sensitive languages.

With Farkle 7's push-based parsing model, a tokenizer that needs more characters it can no longer just read them but it has to return and wait for the user to provide the characters. This presents some unique challenges:

* When the parsing process is resumed, we want to support resuming the tokenizer at a specific point in its logic. Always restarting it from the end of the last token is both inefficient and impossible in some cases like noise groups where input gets consumed but no tokens are produced.

* Tokenizers could store information in the `ParserState` that would guide them to the right point when resuming, but this poses another problem: if we have many tokenizers, likely written by different people, how would these interact? In general, how would one tokenizer defer to another one? In Farkle 6 this was done by subclassing `DefaultTokenizer` and calling `base.Tokenize()`, but it won't work here, because inheritance is a very inflexible extension mechanism, and supporting resuming to tokenizers at arbitrary call depths is not practical.

* Even in cases of success a tokenizer would want to resume at a specific point at the next invocation, like if it wants to emit many tokens consecutively and does not want to be distracted by other tokenizers.

* And all that has to be invisible by the parser that is using the tokenizer.

## The idea

To satisfy the above requirements, Farkle 7 will introduce the concept of _tokenizer chaining_ as the means to extend the tokenization logic. Imagine that you are writing a parser for an indentation-sensitive language. Instead of your custom tokenizer that handles indentation and explicitly invokes the default tokenizer when needed, you define _at creation time_ a chain of tokenizers, and Farkle will invoke them in order, until one of them returns a result. In your case, the chain would consist of your custom tokenizer first, and Farkle's default tokenizer at the end.

This arrangement of tokenizers imposes some constraints -like tokenizers not being able to directly invoke others-, but it comes to shine when a tokenizer needs more input. Imagine that you have a chain of three tokenizers. The first is invoked, returns without finding a token, and Farkle invokes the second one. The second tokenizer thinks it is into something, but needs more input to be 100% sure. If it just returned, Farkle would give a chance to the final tokenizer, which will very likely ruin its predecessor's potential catch. Before returning, we want the second tokenizer to be able to _suspend_ the tokenizing process, which would prevent its succeeding tokenizer from being invoked, and when the parser resumes, the second tokenizer will be the first one to be invoked. Every piece of the chain becomes a safe point for resuming the tokenizing process.

## The code

### Creating tokenizers

To be created, a tokenizer needs the grammar of the language it will operate on. Farkle 6 had the `TokenizerFactory` class with an abstract `Create` method that accepted a grammar and returned a tokenizer. For Farkle 7 we want to be slightly more flexible, as well as support building chained tokenizers:

```csharp
namespace Farkle.Parser.Tokenizers;

public sealed class ChainedTokenizerBuilder<TChar>
{
    public static ChainedTokenizerBuilder<TChar> Create(Tokenizer<TChar> tokenizer);

    public static ChainedTokenizerBuilder<TChar> Create(
        Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory);

    // Starts the chain with the existing tokenizer of a CharParser.
    public static ChainedTokenizerBuilder<TChar> CreateDefault();

    // The Append methods are immutable and return a new builder with the new tokenizer appended.
    public ChainedTokenizerBuilder<TChar> Append(Tokenizer<TChar> tokenizer);

    public ChainedTokenizerBuilder<TChar> Append(
        Func<IGrammarProvider, Tokenizer<TChar>> tokenizerFactory);

    public ChainedTokenizerBuilder<TChar> Append(ChainedTokenizerBuilder<TChar> builder);

    public ChainedTokenizerBuilder<TChar> AppendDefault();

    // If grammar is null, IGrammarProvider.GetGrammar will throw in the tokenizer factories.
    // If defaultTokenizer is null, using ChainedTokenizerBuilder.Default in the chain will throw.
    public Tokenizer<TChar> Build(IGrammarProvider? grammar = null, Tokenizer<TChar>? defaultTokenizer = null);
}

namespace Farkle;

public abstract partial class CharParser<T>
{
    // Already defined at parser-api.md:
    // public CharParser<T> WithTokenizer(Tokenizer<T> tokenizer);

    public CharParser<T> WithTokenizer(
        Func<IGrammarProvider, Tokenizer<T>> tokenizerFactory);

    public CharParser<T> WithTokenizer(ChainedTokenizerBuilder<T> builder);
}
```

Besides simple tokenizer objects, the tokenizer of a `CharParser` can be changed by providing a _tokenizer factory_ or a _chained tokenizer builder_.

A tokenizer factory is a delegate that accepts a `IGrammarProvider` and returns a tokenizer. We use `IGrammarProvider` instead of just `Grammar` to allow in the future looking up the special names without depending on the entire grammar API.

A chained tokenizer builder builds a chain of tokenizers from the start to the end and can be either passed to a `CharParser` or used standalone. Each component of a chained tokenizer builder can be a tokenizer, a tokenizer factory or another chained tokenizer builder. The `Default` property of `ChainedTokenizerBuilder` is a builder that starts with the existing tokenizer of a `CharParser` as its only component. The `AppendDefault` method appends that default tokenizer to the chain. `Build` creates a wrapper object that runs the tokenizers in sequence as described below.

### Suspending tokenizers

We will provide the following APIs to support suspending the tokenization process:

```csharp
namespace Farkle.Parser.Tokenizers;

public interface ITokenizerResumptionPoint<TChar, in TArg>
{
    bool TryGetNextToken(ref ParserInputReader<TChar> input, ITransformer<TChar> transformer, TArg arg, out TokenizerResult token);
}

public static class TokenizerExtensions
{
    public static void SuspendTokenizer<TChar>(this ref ParserInputReader<TChar> input,
        Tokenizer<TChar> tokenizer);
    public static void SuspendTokenizer<TChar, TArg>(this ref ParserInputReader<TChar> input,
        ITokenizerResumptionPoint<TChar, TArg> suspensionPoint, TArg argument);
}
```

There are two use cases for suspension. A tokenizer that needs more input can call `SuspendTokenizer` in `TryGetNextToken` and return `false`, and when parsing resumes the chain will continue from that tokenizer.

Alternatively a tokenizer that has found a token and wants to keep finding for potentially another token can call `SuspendTokenizer` in `TryGetNextToken` and return `true`. The tokenizer chain does not continue when a tokenizer finds a token either way, but when the tokenizer chain is invoked again it will not start over.

The arguments to the `SuspendTokenizer` methods determine where the chain will continue from. Besides a `Tokenizer<TChar>` we can resume the tokenization process to an object of type `ITokenizerResumptionPoint`. This interface provides a very similar API to `Tokenizer`, but also accepts an argument of type `TArg`, giving some more flexibility to tokenizer authors. A tokenizer can implement this interface many times with different types for `TArg` to support different resumption points. Here's an example:

```csharp
public class MyTokenizer : Tokenizer<char>, ITokenizerResumptionPoint<char, MyTokenizer.Case1Args>,
    ITokenizerResumptionPoint<char, MyTokenizer.Case2Args>
{
    public override bool TryGetNextToken(ref ParserInputReader<char> input,
        ITransformer<char> transformer, out TokenizerResult token)
    {
        if (/* case 1 */)
        {
            inputReader.SuspendTokenizer(this, new Case1Args(/* … */));
            token = default;
            return false;
        }
        else if (/* case 2 */)
        {
            inputReader.SuspendTokenizer(this, new Case2Args(/* … */));
            token = default;
            return false;
        }
        else
        {
            // …
        }
    }

    bool ITokenizerResumptionPoint<char, Case1Args>.TryGetNextToken(ref ParserInputReader<char> input,
        ITransformer<char> transformer, Case1Args arg, out TokenizerResult token)
    {
        // Case 1 resumes here with more characters.
        // …
    }

    bool ITokenizerResumptionPoint<char, Case2Args>.TryGetNextToken(ref ParserInputReader<char> input,
        ITransformer<char> transformer, Case2Args arg, out TokenizerResult token)
    {
        // Case 2 resumes here with more characters.
        // …
    }

    private struct Case1Args { /* … */ }
    private struct Case2Args { /* … */ }
}
```

After the resumed tokenizer or resumption point are ran, the tokenizer chain continues from the next tokenizer after the one that had invoked `SuspendTokenizer`. The exact tokenizer or tokenizer resumption point instance passed to `SuspendTokenizer` does not matter; Farkle itself keeps track of the index of the running tokenizer in the chain.

### Composability guidelines

In order to allow multiple tokenizers to interact with each other, tokenizer implementations must not greedily read all input until they can find a token, and must instead stop with returning `false` after encountering a noise symbol. This is a change from Farkle 6.

To understand why this is necessary, consider a simple indentation-based grammar with line comments. Its parser has a chain with two tokenizers; one stateful that handles the indentation level changes at the beginning of each line, and the default stateless tokenizer that parses the comments and the text of each line.

Let's parse the following input:

```
foo # Comment
    bar
```

The following steps would happen if each tokenizer in the chain was greedy. Each step starts with the line and column number the parser input reader is at:

1. (1,1) The stateful tokenizer sees `foo` without any spaces before it, decides to not emit an indentation start token and returns `false`.
2. (1,1) The stateless tokenizer reads `foo ` and stops at the line comment start. It emits a token, consumes `foo `, and returns `true`.
3. (1,5) The stateful tokenizer runs and returns immediately because it's not in the beginning of a line.
4. (1,5) The stateless tokenizer reads `# Comment` and consumes it. __But it continues__ and consumes the newline character, and the spaces, until it finds `bar` which consumes as well, emitting a token, and returning `true`.

The stateful tokenizer did not have the opportunity to see that the second line was indented, and produced an incorrect result. If the stateless tokenizer returned `false` after consuming the comment and the newline, the stateful tokenizer would have been able to see the indentation.

However, this yielding on noise symbols might have a performance impact, as the tokenizer's `TryGetNextToken` method will have to be called more times. To mitigate this, we introduce an extension method that allows tokenizers to check whether they are the only ones in the chain, and thus are allowed (but not mandated) to be greedy:

```csharp
namespace Farkle.Parser.Tokenizers;

public static class TokenizerExtensions
{
    public static bool IsSingleTokenizerInChain<TChar>(this in ParserInputReader<TChar> input);
}
```

In order to simplify the consumers of the lazy tokenizers and spare them from having to determine why the tokenizer returned `false` (because it suspended, it needs more input, or it encountered a noise symbol), the chained tokenizer wrapper will itself be greedy, returning `false` only when:

* One of the tokenizers suspends and returns `false`.
* All tokenizers in the chain return `false` and none of them has `Consume`d any input characters.
    * If the `IsFinalBlock` property is true, this will signify the end of the input.

Having the same class specify a different contract for implementers and consumers is arguably confusing, but this is the best design that satisfies all requirements of composability and performance (we could add a separate `ITokenizerChainPart` interface for chain parts that are allowed to be greedy, but I'm wary of using an interface due to potential overhead). In order to mitigate the confusion, all `Tokenizer<TChar>` instances returned by Farkle must be wrapped in a chain.

### Suspending a standalone tokenizer

If you have a chain of many tokenizers, they are wrapped into one tokenizer object that runs them in sequence and supports suspending. But if you have a single tokenizer, what happens if it suspends? Invoking it again by calling `TryGetNextToken` will directly call the tokenizer's regular entry point without giving the opportunity for something to call a custom resuming tokenizer or resumption point.

The solution to this is to wrap even single tokenizers into a chain. `ChainedTokenizerBuilder.Build` will not take a shortcut if it contains only one tokenizer, and if `CharParser<T>.WithTokenizer` is provided a `Tokenizer<char>` that is not a chained one, it will automatically be wrapped in one. This will introduce one extra layer of indirection but will ensure that suspension always works. We could introduce an API to allow tokenizers to declare that they will never suspend and always read `IsSingleTokenizerInChain`, and thus don't have to be wrapped.

Another way to avoid the indirection is to add the following API to `ParserInputReader` and require parsers to call it at the start of a parsing operation:

```csharp
public static class TokenizerExtensions
{
    public static void ProcessSuspendedTokenizer(this ref ParserInputReader<TChar> input);
}
```

This idea was rejected because it would add more conventions to an already convention-based API, would break the transparency requirement of the suspension mechanism, and the benefits are tiny and situational.

## Alternative designs

### Rejected - `await`ing for more characters

The need for chaining and suspending tokenizers would have been eliminated if we could do something like `await reader.WaitForMoreCharactersAsync()`. To avoid making the entire parser asynchronous, the tokenizer's entry point would return a special type with a custom async method builder that supports only `await`ing the method above, which would suspend the tokenizer and set a very precise resumption point.

The advantage of this approach is that we can compose tokenizers in arbitrary ways just like in Farkle 6 and provide a very intuitive and much smaller API set. The disadvantage is that it would be bad for performance since the parser state must be stored in the heap and preclude us from supporting parsing spans.

### More complex chaining

The "flat" chaining model described above is quite primitive. There were thoughts to support more complex chains, with components that act as "filters" where they can inspect and potentially change the result of a part of the chain. One use case would be to handle tokenizer failures, but the whole feature needs more thought and was postponed for a version after 7.0.
