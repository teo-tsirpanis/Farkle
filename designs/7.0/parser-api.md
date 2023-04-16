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

[^antlr]: Contrast this with solutions like ANTLR [where you need six lines to parse a string and you are not even done yet](https://github.com/antlr/antlr4/blob/master/doc/csharp-target.md#how-do-i-use-the-runtime-from-my-project). Sure it's super flexible but the barrier of entry is super high. And creating a grammar has an even higher barrier.

[^parser]: By _parser_ here we mean the entire pipeline consisting of the tokenizer, the LR parser and the semantic analyzer.
