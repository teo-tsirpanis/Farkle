# Advanced features

Farkle has a couple of features that were not covered in other guides because they are less likely to be useful. In this guide, we will take a look at some of these features that truly push Farkle's capabilities to the fullest. So, are you ready? Let's do this!

## The untyped API

The first feature we are going to discuss is a different API for creating grammars, which is called the untyped API. Usually, when we write our designtime Farkles, we do not only state how our grammar is structured, but also what should our parser do when he encounters a specific symbol. There are times however that we do not want to do the latter thing: we just want Farkle to create a grammar. Let's take a look at the following grammar which matches matching parentheses:

```
<S> ::= <S> ( <S> )
<S> ::= <>
```

In Farkle, one way to write this grammar, is to just write it, but return dummy types like units on every production:

``` fsharp
// In all our examples, we always open this namespace.
open Farkle.Builder

let S = nonterminal "S"

S.SetProductions(
    !% S .>> "(" .>> S .>> ")" =% (),
    empty =% ()
)
```

On larger grammars however, this habit of adding `=% ()` to the end of each production tends to be repetitive. Also imagine if we had more complex terminals. We would have done something like this:

``` fsharp
let X =
    myComplexRegex
    |> terminal "X" (T(fun _ _ -> ()))
```

The parentheses-ridden delegate definition at the end would have to be repeated, for every terminal we would have to create.

For this reason, the untyped API was created, to minimize code duplication. Let's first see how we would write our terminal:

__F#:__
``` fsharp
let X =
    myComplexRegex
    |> terminalU "X"
```

__C#:__
``` csharp
DesigntimeFarkle X = Terminal.Create("X", myComplexRegex);
```

In F#, we use the `terminalU` function (guess what the U stands for), and in C# we just omit the delegate (it would have otherwise appeared between the name and the regex).

As you might have seen, the terminal is of type `DesigntimeFarkle`, without a generic parameter at the end. This means it can be normally used from other grammars (even typed), but it cannot be the significant member of a production. You can write for example `!@ W .>> X` (or `W.Extended().Append(X)` in C#), but not `!@ W .>>. X` (or `W.Extended().Extend(X)`).

> If you don't remember how to use an API from C#, [this guide](csharp.html) can help you.

### Defining untyped nonterminals

The nonterminals use a slightly different approach. Let's see how we would write the nonterminal that recognizes the balanced parentheses:

__F#:__
``` fsharp
let S = nonterminalU "S"

S.SetProductions(
    !% S .>> "(" .>> S .>> ")",
    empty
)
```

__C#:__
``` csharp
var S = Nonterminal.CreateUntyped("S");

S.SetProductions(
    new ProductionBuilder(S, "(", S, ")"),
    ProductionBuilder.Empty
);
```

For F# we use the `nonterminalU` function to define an untyped nonterminal, and after that we set its productions, just like the typed nonterminals. We use the familiar production builders syntax to do it but with some changes: we always chain the members of the production with the `.>>` operator since none of its members are significant, and we don't finish the production builder in the end with `=>` or `=%`. `S` is of type `Farkle.Builder.Untyped.Nonterminal`, which implements only the untyped `DesigntimeFarkle` interface.

For C# we could use production builders without extending or finishing them like in F#, but there is another shorter way. We use the `ProductionBuilder`'s constructor which accepts a variable amount of objects (or none, but this is essentially the empty one). You can pass designtime Farkles to be used in the resulting production as they are, or strings or characters to be used as literals. To avoid boxing, it's better to not pass characters at all, but not prohibited. If you pass any other type, an exception will be thrown.

In earlier versions of Farkle we could not reliably use the production builder syntax due to a compiler limitation. If you are getting weird syntax errors about type mismatches, use the `ProductionBuilder`'s constructor instead.

---

To show how to define non-recursive productions, let's take a look at a different example. Consider this F# designtime Farkle:

``` fsharp
let number = Terminals.uint32 "Number"

let adder = "Add" ||= [!@ number .>> "+" .>>. number => (+)]
```

It does exactly what you think it does. It gets a string of the form `X + Y`, and returns an unsigned integer containing their sum.

A grammar that recognizes the same language without returning anything can be defined like this:

``` fsharp
let number = Terminals.uint32 "Number"

let adder = "Add" |||= [!% number .>> "+" .>> number]
```

There are four differences in the untyped terminal. We use the `|||=` operator instead of `||=`, `!%` instead of `!@`, `.>>` instead of `.>>.` and omit finishing the production builder with `=>`. In C# we can do the same thing with the `ProductionBuilder` like that:

``` csharp
DesigntimeFarkle<uint> Number = Terminals.UInt32("Number");
DesigntimeFarkle Adder = Nonterminal.CreateUntyped("Adder", new ProductionBuilder(Number, "+", Number))
```

### Building untyped designtime Farkles

Building these untyped designtime Farkles is actually surprisingly simple and can be done this way:

__F#:__
``` fsharp
// This is of type `RuntimeFarkle<unit>`.
let adderRuntime = RuntimeFarkle.buildUntyped adder
```

__C#:__
``` csharp
// The object it returns will always be null.
RuntimeFarkle<object> AdderRuntime = Adder.BuildUntyped();
```

`buildUntyped` creates a `RuntimeFarkle` that does not return anything meaningful, and succeeds if the input text is valid. On F# it returns a unit and on C# an object that is always `null`.

## Syntax checking

It is sometimes useful to just check if a string is syntactically valid, instead of giving it a meaning by returning an object out of it.

This is what the untyped API does, and we can call `buildUntyped` on a typed designtime Farkle to achieve the same.

Because building a designtime Farkle is expensive, if we already have a runtime Farkle, we can create a new one with the same grammar, but with a post-processor that does nothing. This post-processor is called a _syntax checker_. We can change the post-processor of a runtime Farkle this way:

__F#:__
``` fsharp
open Farkle.PostProcessor

let designtime: DesigntimeFarkle<int> = foo()

let runtime: RuntimeFarkle<int> = RuntimeFarkle.build designtime

// syntaxChecker is of type RuntimeFarkle<unit>.
let syntaxCheck = RuntimeFarkle.changePostProcessor PostProcessors.syntaxCheck runtime
```

__C#:__
``` csharp
DesigntimeFarkle<int> Designtime = Foo();

RuntimeFarkle<int> Runtime = Designtime.Build();

RuntimeFarkle<object> SyntaxCheck = Runtime.SyntaxCheck();
// or
RuntimeFarkle<Unit> SyntaxCheck = Runtime.ChangePostProcessor(PostProcessors.SyntaxChecker);
```

Changing the post-processor is extremely cheap; no new grammar objects are created, and the syntax-checking post-processor is the same.

> Actually, the post-processor used in both the F# and the C# example is the same object too. Post-processors are [covariant][covariance] like designtime Farkles, because they are interfaces. Runtime Farkles on the other hand are classes and therefore not variant at all.

## Custom tokenizers

Farkle's default tokenizer is relatively simple. It splits the input text into tokens, without any regard for these tokens' location. There are some more complex grammars however that need a smarter tokenizer, for example indentation-based languages like F# and Python. Using Farkle's standard facilities is not enough to determine when a block begins or ends.

For these advanced cases, Farkle provides a way to write additional tokenizing logic on top of the default tokenizer. There is [an extensively commented sample][indent-based] where we write a parser for a simple indentation-based language. Our indentation level detection logic only kicks in when we are at the beginning of a line, and we defer to Farkle's default tokenizer to take care of the rest.

---

Farkle has more APIs for various little features that would make this document too lengthy. Fortunately, [they are well-documented in this site](reference/index.html), as well as while you code thanks to IntelliSense.

So, I hope you enjoyed this little guide. If you did, don't forget to give Farkle a try, and maybe you feel especially untyped today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[covariance]: https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/covariance-contravariance/
[indent-based]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/IndentBased.fs
