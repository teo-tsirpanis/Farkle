# Farkle 7's tokenizer API

Since version 6.0 Farkle supports customizing the logic of breaking the input text into a sequence of tokens, known as _tokenizing_. This is used to implement parsers for languages that cannot be tokenized with a simple DFA, such as indentation-sensitive languages.

With Farkle 7's push-based parsing model, a tokenizer that needs more characters it can no longer just read them but it has to return and wait for the user to provide the characters. This presents some unique challenges:

* When the parsing process is resumed, we want to support resuming the tokenizer at a specific point in its logic. Always restarting it from the end of the last token is both inefficient and impossible in some cases like noise groups where input gets consumed but no tokens are produced.

* Tokenizers could store information in the `ParserState` that would guide them to the right point when resuming, but this poses another problem: if we have many tokenizers, likely written by different people, how would these interact? In general, how would one tokenizer defer to another one? In Farkle 6 this was done by subclassing `DefaultTokenizer` and calling `base.Tokenize()`, but it won't work here, because inheritance is a very inflexible extension mechanism, and supporting resuming to tokenizers at arbitrary call depths is not practical.

* Even in cases of success a tokenizer would want to resume at a specific point at the next invocation, like if it wants to emit many tokens consecutively and does not want to be distracted by other tokenizers.

## The idea

To satisfy the above requirements, Farkle 7 will introduce the concept of _tokenizer chaining_ as the means to extend the tokenization logic. Imagine that you are writing a parser for an indentation-sensitive language. Instead of your custom tokenizer that handles indentation explicitly invoking the default tokenizer, you define _at creation time_ a chain of tokenizers, and Farkle will invoke them in order, until one of them returns a result. In your case, the chain would consist of your custom tokenizer first, and Farkle's default tokenizer at the end.

This arrangement of tokenizers imposes some constraints -like tokenizers not being able to directly invoke others-, but it comes to shine when a tokenizer needs more input. Imagine that you have a chain of three tokenizers. The first is invoked, returns without finding a token, and Farkle invokes the second one. The second tokenizer thinks it is into something, but needs more input to be 100% sure. If it just returned, Farkle would give a chance to the final tokenizer, which will very likely ruin its predecessor's potential catch. Before returning, we want the second tokenizer to be able to _suspend_ the tokenizing process, which would prevent its succeeding tokenizer from being invoked, and when the parser resumes, the second tokenizer will be the first one to be invoked. Every piece of the chain becomes a safe point for resuming the tokenizing process.

## The code

### Creating tokenizers

To be created, a tokenizer needs the grammar of the language it will operate on. Farkle 6 had the `TokenizerFactory` class with an abstract `Create` method that accepted a grammar and returned a tokenizer. For Farkle 7 we want to be slightly more flexible, as well as support building chained tokenizers:

```csharp
namespace Farkle.Parser.LexicalAnalysis;

public readonly struct TokenizerFactoryContext
{
    public TokenizerFactoryContext(Grammar? grammar);

    // These two methods will fail if the grammar in the constructor
    // or the ChainedTokenizerBuilder.Build method is null. They will
    // never fail when creating a tokenizer for a CharParser since it
    // guarantees that a grammar exists.
    public Grammar GetGrammar();
    public EntityHandle GetSymbolFromSpecialName(string name, bool throwIfNotFound = false);
}

public sealed class ChainedTokenizerBuilder<TChar>
{
    // A placeholder for the existing tokenizer of a CharParser.
    public static ChainedTokenizerBuilder<TChar> Default { get; }

    public static ChainedTokenizerBuilder<TChar> Create(Tokenizer<TChar> tokenizer);

    public static ChainedTokenizerBuilder<TChar> Create(
        Func<TokenizerFactoryContext, Tokenizer<TChar>> tokenizerFactory);

    // The Append methods are immutable and return a new builder with the new tokenizer appended.
    public ChainedTokenizerBuilder<TChar> Append(Tokenizer<TChar> tokenizer);

    public ChainedTokenizerBuilder<TChar> Append(
        Func<TokenizerFactoryContext, Tokenizer<TChar>> tokenizerFactory);

    public ChainedTokenizerBuilder<TChar> Append(ChainedTokenizerBuilder<TChar> builder);

    public ChainedTokenizerBuilder<TChar> AppendDefault();

    // If grammar is null, TokenizerFactoryContext.GetGrammar will throw in the tokenizer factories.
    // If defaultTokenizer is null, using ChainedTokenizerBuilder.Default in the chain will throw.
    public Tokenizer<TChar> Build(Grammar? grammar = null, Tokenizer<TChar>? defaultTokenizer = null);
}

namespace Farkle;

public abstract partial class CharParser<T>
{
    // Already defined at parser-api.md:
    // public abstract CharParser<T> WithTokenizer(Tokenizer<T> tokenizer);

    public abstract CharParser<T> WithTokenizer(
        Func<TokenizerFactoryContext, Tokenizer<T>> tokenizerFactory);

    public abstract CharParser<T> WithTokenizer(ChainedTokenizerBuilder<T> builder);
}
```

Besides simple tokenizer objects, the tokenizer of a `CharParser` can be changed by providing a _tokenizer factory_ or a _chained tokenizer builder_.

A tokenizer factory is a delegate that accepts a `TokenizerFactoryContext` and returns a tokenizer. We use `TokenizerFactoryContext` instead of just `Grammar` to allow in the future looking up the special names without depending on the entire grammar API.

A chained tokenizer builder builds a chain of tokenizers from the start to the end and can be either passed to a `CharParser` or used standalone. Each component of a chained tokenizer builder can be a tokenizer, a tokenizer factory or another chained tokenizer builder. The `Default` property of `ChainedTokenizerBuilder` is a builder that starts with the existing tokenizer of a `CharParser` as its only component. The `AppendDefault` method appends that default tokenizer to the chain.
