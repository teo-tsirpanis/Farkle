# Tutorial: Creating a calculator

Hello everyone. This tutorial will guide you through the process of creating a simple calculator using Farkle, using either C# or F#. Familiarity with context-free grammars and parsing will be helpful but not required.

## Setting up

You can install Farkle from [NuGet][nuget]. One of the ways to do this is by using the following command:

```
dotnet add package Farkle --prerelease
```

## Parsing a number

Let's start by teaching Farkle how to parse decimal numbers. We can do this by defining a _terminal_. A terminal is a symbol that can be directly matched from the input text.

# [C#](#tab/csharp)

```csharp
using System.Globalization;
using Farkle;
using Farkle.Builder;

IGrammarSymbol<double> number = Terminal.Create("Number", Regex.FromRegexString(@"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"),
    (ref _, input) => double.Parse(input, CultureInfo.InvariantCulture));
```

If you are using a version earlier than C# 14, the code is slightly more verbose:

```csharp
using System.Globalization;
using Farkle;
using Farkle.Builder;
using Farkle.Parser;

IGrammarSymbol<double> number = Terminal.Create("Number", Regex.FromRegexString(@"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"),
    (ref ParserState _, ReadOnlySpan<byte> input) => double.Parse(input, CultureInfo.InvariantCulture));
```

# [F#](#tab/fsharp)

```fsharp
open System
open System.Globalization
open Farkle
open Farkle.Builder

let number =
    Regex.regexString "[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"
    |> terminal "Number" (T(fun _ data -> Double.Parse(data, CultureInfo.InvariantCulture)))
```

---

As you can see, the terminal is made of three things:

1. A _name_, which is used for informative and diagnostic purposes. The name can be empty, and a grammar can have multiple terminals with the same name.
2. A _regular expression_, which is used to match the input text. In this example, we define the regular expression with a [string pattern](string-regexes.md).
3. A _transformer_, which is a function that is used to convert the matched text into a semantic value. Due to language limitations in F#, we wrap the transformer in `T`, which is a shortened alias for the transformer's delegate type.

> [!NOTE]
> Farkle's regular expressions have a type of `Farkle.Builder.Regex`, and are distinct from .NET's `System.Text.RegularExpressions.Regex`.

> [!TIP]
> You can think of a transformer as a "mini-parser", with the advantage of being easier to write, because its input is always correct and matching the terminal's regular expression.

## Parsing two numbers

Now that we have a terminal for numbers, we can use it to parse something more complex, by defining a _nonterminal_. A nonterminal is a symbol that can be matched with a sequence of zero or more terminals and nonterminals. There are many sequences that can match a nonterminal, and each of these is called a production rule, or simply _production_.

Let's define a nonterminal for an arithmetic expression with up to two numbers, supporting the four elementary operations, and unary negation. In [Backus-Naur form][bnf], this is the grammar we want to define:

```
<Expression> ::= number + number
<Expression> ::= number - number
<Expression> ::= number * number
<Expression> ::= number / number
<Expression> ::= number ^ number
<Expression> ::= - number
<Expression> ::= number
<Expression> ::=
```

Here's how we can write this grammar in Farkle:

# [C#](#tab/csharp)

```csharp
// number terminal defined above

IGrammarSymbol<double> expression = Nonterminal.Create("Expression",
    number.Extended().Append("+").Extend(number).Finish((x1, x2) => x1 + x2),
    number.Extended().Append("-").Extend(number).Finish((x1, x2) => x1 - x2),
    number.Extended().Append("*").Extend(number).Finish((x1, x2) => x1 * x2),
    number.Extended().Append("/").Extend(number).Finish((x1, x2) => x1 / x2),
    number.Extended().Append("^").Extend(number).Finish((x1, x2) => Math.Pow(x1, x2)),
    "-".Appended().Extend(number).Finish(x => -x),
    number.AsProduction(),
    ProductionBuilder.Empty.FinishConstant(0.0));
```

# [F#](#tab/fsharp)

```fsharp
// number terminal defined above

let expression = "Expression" ||= [
    !@ number .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
    !@ number .>> "-" .>>. number => (fun x1 x2 -> x1 - x2)
    !@ number .>> "*" .>>. number => (fun x1 x2 -> x1 * x2)
    !@ number .>> "/" .>>. number => (fun x1 x2 -> x1 / x2)
    !@ number .>> "^" .>>. number => (fun x1 x2 -> System.Math.Pow(x1, x2))
    !& "-" .>>. number => (fun x -> -x)
    !@ number |> asProduction
    empty =% 0.0
]
```

---

As you can see, the nonterminal is made of three things:

1. A _name_, which like terminals, is used for informative and diagnostic purposes. The name can be empty, and a grammar can have multiple terminals and nonterminals with the same name.
2. A sequence of one or more _productions_, which define how the nonterminal can be matched in the input text. The productions are made of two things:
   1. A sequence of _members_, which can be terminals or nonterminals.
   2. A _fuser_, which is a function that combines the semantic values of the production's members, and produces the semantic value of the nonterminal itself.

The `+`, `-`, `*`, `/`, and `^` operators are a special case of terminals called _literals_. A literal is matched by a literal string, and Farkle lets you use that string directly, instead of explicitly defining a terminal.

We use a special syntax to define productions and fusers in a type-safe way that is idiomatic for each of C# and F#.

# [C#](#tab/csharp)

We define the productions using a fluent and type-safe API. We start building the production by calling `Appended` or `Extended` on its first member, add additional members with `Append` or `Extend`, and finally call `Finish()` with the production's fuser.

The difference between the `Extend` and `Append` family of methods, is that every call to `Extend` will add a new parameter to the production's fuser, while `Append` will not.

There exist some shortcuts for common patterns. If a production returns a constant value, we can provide it with the `FinishConstant` method, and if a production returns a single value unchanged, we can use `AsProduction`.

We use `ProductionBuilder.Empty` to start building a production with no members.

# [F#](#tab/fsharp)

We define the productions using a type-safe domain-specific language (DSL). We use several different operators to start building a production, add additional members, and finally call `=>` with the production's fuser.

|Operator|Description|Adds parameter to fuser|
|-|-|-|
|`!@`|Starts a production with a symbol (terminal or nonterminal)|Yes|
|`!%` (not used here)|Starts a production with a symbol|No|
|`!&`|Starts a production with a string|No|
|`.>>.`|Adds a symbol to the production|Yes|
|`.>>`|Adds a symbol or string to the production|No|

There exist some shortcuts for common patterns. If a production returns a constant value, we can use the `=%` operator, and if a production returns a single value unchanged, we can use `asProduction`.

We use `empty` to start building a production with no members.

---

## Interlude: Running a parser

Before we continue, let's see how to actually use the grammars we defined to parse some text. We cannot do this directly; we first need to _build_ a grammar. Building a grammar gathers a list of all symbols and productions, validates that there are no ambiguities, and produces the necessary data structures to efficiently parse text.

# [C#](#tab/csharp)

Building a grammar is as easy as calling the `Build()` method on a symbol object. This method will return a parser object that can parse that symbol. Let's see how to do that:

```csharp
CharParser<double> parser = expression.Build();

Console.WriteLine(parser.Parse("")); // 0
Console.WriteLine(parser.Parse("1")); // 1
Console.WriteLine(parser.Parse("- 9")); // -9
Console.WriteLine(parser.Parse("1 + 1")); // 2
Console.WriteLine(parser.Parse("8.5 - 3")); // 5.5
Console.WriteLine(parser.Parse("4.5 * 2")); // 9
Console.WriteLine(parser.Parse("6 / 2")); // 3
Console.WriteLine(parser.Parse("6 ^ 2")); // 36
Console.WriteLine(parser.Parse("6 /")); // (1, 4) Found (EOF), expected Number
```

# [F#](#tab/fsharp)

Building a grammar is as easy as passing a symbol object to the `GrammarBuilder.build` function. This method will return a parser object that can parse that symbol. Let's see how to do that:

```fsharp
let parser = GrammarBuilder.build expression

CharParser.parseString parser "" |> printfn "%O" // 0
CharParser.parseString parser "1" |> printfn "%O" // 1
CharParser.parseString parser "- 9" |> printfn "%O" // -9
CharParser.parseString parser "1 + 1" |> printfn "%O" // 2
CharParser.parseString parser "8.5 - 3" |> printfn "%O" // 5.5
CharParser.parseString parser "4.5 * 2" |> printfn "%O" // 9
CharParser.parseString parser "6 / 2" |> printfn "%O" // 3
CharParser.parseString parser "6 ^ 2" |> printfn "%O" // 36
CharParser.parseString parser "6 /" |> printfn "%O" // (1, 4) Found (EOF), expected Number
```

---

A parser accepts a string (or [other types of input](../api/Farkle.ParserExtensions.yml)), and after running the transformers and fusers of the symbols it encounters, it produces a result and returns it to the user. The parser's result is wrapped in a [special type](../api/Farkle.ParserResult-1.yml), that also contains any errors that might have occurred during parsing.

As you can see, Farkle's grammars ignore whitespace by default.

> [!IMPORTANT]
> Building a grammar is an expensive operation. You should reuse the returned parser object, and not build the same grammar multiple times.

## Parsing many numbers

Moving on, let's write a grammar for an arbitrarily long expression of the four elementary operations; we will leave out exponentiation and unary negation for now. This is the BNF for our grammar:

```
<Expression> ::= <Expression> + number
<Expression> ::= <Expression> - number
<Expression> ::= <Expression> * number
<Expression> ::= <Expression> / number
<Expression> ::= number
```

We use recursion to make the nonterminal arbitrarily long, with the `<Expression> ::= number` production being the base case. Defining a recursive nonterminal in Farkle will be slightly different; instead of defining the nonterminal and its productions at once, we first create the nonterminal, and set its productions later. Let's see how to do that:

# [C#](#tab/csharp)

```csharp
using Farkle;
using Farkle.Builder;

// number terminal defined above

Nonterminal<double> expression = Nonterminal.Create<double>("Expression");
expression.SetProductions(
    expression.Extended().Append("+").Extend(number).Finish((x1, x2) => x1 + x2),
    expression.Extended().Append("-").Extend(number).Finish((x1, x2) => x1 - x2),
    expression.Extended().Append("*").Extend(number).Finish((x1, x2) => x1 * x2),
    expression.Extended().Append("/").Extend(number).Finish((x1, x2) => x1 / x2),
    number.AsProduction());

CharParser<double> parser = expression.Build();

Console.WriteLine(parser.Parse("1 + 2 + 3")); // 6
Console.WriteLine(parser.Parse("4 * 5 + 6")); // 26
Console.WriteLine(parser.Parse("4 * 5 +")); // (1, 8) Found (EOF), expected Number
```

# [F#](#tab/fsharp)

```fsharp
open Farkle
open Farkle.Builder

// number terminal defined above

let expression = nonterminal "Expression"
setProductions expression [
    !@ expression .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
    !@ expression .>> "-" .>>. number => (fun x1 x2 -> x1 - x2)
    !@ expression .>> "*" .>>. number => (fun x1 x2 -> x1 * x2)
    !@ expression .>> "/" .>>. number => (fun x1 x2 -> x1 / x2)
    !@ number |> asProduction]

let parser = GrammarBuilder.build expression

CharParser.parseString parser "1 + 2 + 3" |> printf "%O" // 6
CharParser.parseString parser "4 * 5 + 6" |> printf "%O" // 26
CharParser.parseString parser "4 * 5 +" |> printf "%O" // (1, 8) Found (EOF), expected Number
```

---

## Operator precedence and associativity

Our previous grammar has a flaw; it does not know about operator precedence or associativity, sometimes known in Farkle as _P&A_. Operator precedence means that `1 + 2 * 3` should be parsed as `1 + (2 * 3)`, while our grammar will parse it as `(1 + 2) * 3`. Associativity is whether `1 + 2 + 3` gets parsed as `(1 + 2) + 3` (left associativity) or `1 + (2 + 3)` (right associativity). We want the four elementary operations to be left associative, but exponentiation has to be right associative. While it is possible to directly encode the correct P&A rules in the grammar's syntax, it is tedious, and thankfully, Farkle provides an easy way to define these rules while keeping the syntax simple:

# [C#](#tab/csharp)

```csharp
using Farkle;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;

// number terminal defined above

Nonterminal<double> expression = Nonterminal.Create<double>("Expression");
expression.SetProductions(
    expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
    expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
    expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
    expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
    expression.Extended().Append("^").Extend(expression).Finish((x1, x2) => Math.Pow(x1, x2)),
    "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
    "(".Appended().Extend(expression).Append(")").AsProduction(),
    number.AsProduction());

IGrammarBuilder<double> expressionWithOperators = expression.WithOperatorScope([
    new LeftAssociative("+", "-"),
    new LeftAssociative("*", "/"),
    new PrecedenceOnly(NEG),
    new RightAssociative("^")]);

CharParser<double> parser = expressionWithOperators.Build();

Console.WriteLine(parser.Parse("1 + 2 * 3")); // 7
Console.WriteLine(parser.Parse("(1 + 2) * 3")); // 9
Console.WriteLine(parser.Parse("2 ^ 2 ^ 3")); // 256
Console.WriteLine(parser.Parse("2 ^ 2 ^")); // (1, 8) Found (EOF), expected '-', '(', Number
```

# [F#](#tab/fsharp)

```fsharp
open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence

// number terminal defined above

let NEG = obj()

let expression = nonterminal "Expression"
setProductions expression [
    !@ expression .>> "+" .>>. expression => (fun x1 x2 -> x1 + x2)
    !@ expression .>> "-" .>>. expression => (fun x1 x2 -> x1 - x2)
    !@ expression .>> "*" .>>. expression => (fun x1 x2 -> x1 * x2)
    !@ expression .>> "/" .>>. expression => (fun x1 x2 -> x1 / x2)
    !@ expression .>> "^" .>>. expression => (fun x1 x2 -> System.Math.Pow(x1, x2))
    !@ expression .>> "-" |> prec NEG => (fun x -> -x)
    !& "(" .>>. expression .>> ")" |> asProduction
    !@ number |> asProduction]

let expressionWithOperators =
    expression.WithOperatorScope(OperatorScope(
        LeftAssociative("+", "-"),
        LeftAssociative("*", "/"),
        PrecedenceOnly(NEG),
        RightAssociative("^")))

let parser = GrammarBuilder.build expressionWithOperators

CharParser.parseString parser "1 + 2 * 3" |> printf "%O" // 7
CharParser.parseString parser "(1 + 2) * 3" |> printf "%O" // 9
CharParser.parseString parser "2 ^ 2 ^ 3" |> printf "%O" // 256
CharParser.parseString parser "2 ^ 2 ^" |> printf "%O" // (1, 8) Found (EOF), expected '-', '(', Number
```

---

We use the `WithOperatorScope` extension method to define the grammar's _operator scope_. Each operator scope is made of _associativity groups_, which are groups of operators with the same precedence, and are ordered by their precedence level, ascending. The `WithOperatorScope` method returns object of type `IGrammarBuilder<T>`, which is the base interface of `IGrammarSymbol<T>`. Objects of this type cannot be used in a production, which means that setting the operator scope must be done at the top-most grammar symbol.

The `-` operator is used for both subtraction and unary negation, and has a higher precedence in the latter case. To disambiguate between the two uses, we use the `WithPrecedence` method (or the `prec` function in F#) to link a _contextual precedence token_ to the production, which we later pass to the operator scope, to define the precedence of `-` when used in unary negation. We also use the `PrecedenceOnly` associativity type because associativity in unary operators does not make sense.

## Recap

We have now built a fully functional parser for arbitrarily complex arithmetic expressions with Farkle, supporting operator precedence and associativity, as well as automated error reporting. Let's make a complete program showing everything we've learned:

# [C#](#tab/csharp)

```csharp
using System.Globalization;
using Farkle;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Parser;

IGrammarSymbol<double> number = Terminal.Create("Number", Regex.FromRegexString(@"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"),
    (ref ParserState _, ReadOnlySpan<byte> input) => double.Parse(input, CultureInfo.InvariantCulture));

Nonterminal<double> expression = Nonterminal.Create<double>("Expression");
expression.SetProductions(
    expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
    expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
    expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
    expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
    expression.Extended().Append("^").Extend(expression).Finish((x1, x2) => Math.Pow(x1, x2)),
    "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
    "(".Appended().Extend(expression).Append(")").AsProduction(),
    number.AsProduction());

IGrammarBuilder<double> expressionWithOperators = expression.WithOperatorScope([
    new LeftAssociative("+", "-"),
    new LeftAssociative("*", "/"),
    new PrecedenceOnly(NEG),
    new RightAssociative("^")]);

CharParser<double> parser = expressionWithOperators.Build();

Console.WriteLine("This is a simple mathematical expression parser powered by Farkle.");
Console.WriteLine("Insert your expression and press enter.");

while (Console.ReadLine() is { } input)
{
    Console.WriteLine(parser.Parse(input));
}
```

# [F#](#tab/fsharp)

```fsharp
open System
open System.Globalization
open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence
open Farkle.Parser

let expressionWithOperators =
    let number =
        Regex.regexString "[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"
        |> terminal (T(fun _ input -> Double.Parse(input, CultureInfo.InvariantCulture)))

    let expression = nonterminal "Expression"
    setProductions expression [
        !@ expression .>> "+" .>>. expression => (fun x1 x2 -> x1 + x2)
        !@ expression .>> "-" .>>. expression => (fun x1 x2 -> x1 - x2)
        !@ expression .>> "*" .>>. expression => (fun x1 x2 -> x1 * x2)
        !@ expression .>> "/" .>>. expression => (fun x1 x2 -> x1 / x2)
        !@ expression .>> "^" .>>. expression => (fun x1 x2 -> Math.Pow(x1, x2))
        !& "-" .>>. expression |> prec NEG => (fun x -> -x)
        !& "(" .>>. expression .>> ")" |> asProduction
        !@ number |> asProduction]

    expression.WithOperatorScope(OperatorScope(
        new LeftAssociative("+", "-"),
        new LeftAssociative("*", "/"),
        new PrecedenceOnly(NEG),
        new RightAssociative("^")))

let parser = GrammarBuilder.build expressionWithOperators

printfn "This is a simple mathematical expression parser powered by Farkle."
printfn "Insert your expression and press enter."

let rec repl parser =
    match Console.ReadLine() with
    | null -> ()
    | input ->
        input
        |> CharParser.parseString parser
        |> printfn "%O"
        repl parser

repl parser
```

---

## Bonus: Precompiling the grammar

Every time we run the program above, it will spend some time at startup building the grammar. We can drastically reduce this time by using the precompiler, which will build it once at compile time, and embed it to the assembly, so that at runtime, the parsing tables will not have to be computed again.

The steps to use the precompiler are as follows:

1. Install the [`Farkle.Tools.MSBuild` NuGet package](https://www.nuget.org/packages/Farkle.Tools.MSBuild) to your project.
2. Make sure your code is constructing the @"Farkle.Builder.IGrammarBuilder`1" from a static factory method, so that the precompiler can call it.
   * Mark that method with @"Farkle.Builder.PrecompilerInputAttribute".
3. Create a static factory method that returns a @"Farkle.CharParser`1", so that the precompiler can patch to load the precompiled grammar.
   * That method is recommended to simply call @"Farkle.CharParser.MustPrecompile``1", to make sure that the precompiler has run successfully.
   * Mark that method with @"Farkle.Builder.PrecompilerOutputAttribute".

Here's the program above, modified to use the prpecompiler:

# [C#](#tab/csharp)

```csharp
using System.Globalization;
using Farkle;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Parser;

CharParser<double> parser = GetParser();

Console.WriteLine("This is a simple mathematical expression parser powered by Farkle.");
Console.WriteLine("Insert your expression and press enter.");

while (Console.ReadLine() is { } input)
{
    Console.WriteLine(parser.Parse(input));
}

[PrecompilerInput]
static IGrammarBuilder<double> GetGrammarBuilder()
{
   IGrammarSymbol<double> number = Terminal.Create("Number", Regex.FromRegexString(@"[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"),
       (ref ParserState _, ReadOnlySpan<byte> input) => double.Parse(input, CultureInfo.InvariantCulture));

   Nonterminal<double> expression = Nonterminal.Create<double>("Expression");
   expression.SetProductions(
       expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
       expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
       expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
       expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
       expression.Extended().Append("^").Extend(expression).Finish((x1, x2) => Math.Pow(x1, x2)),
       "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
       "(".Appended().Extend(expression).Append(")").AsProduction(),
       number.AsProduction());

   IGrammarBuilder<double> expressionWithOperators = expression.WithOperatorScope([
       new LeftAssociative("+", "-"),
       new LeftAssociative("*", "/"),
       new PrecedenceOnly(NEG),
       new RightAssociative("^")]);

    return expressionWithOperators;
}

[PrecompilerOutput]
static CharParser<double> GetParser() => CharParser.MustPrecompile<double>();
```

# [F#](#tab/fsharp)

```fsharp
open System
open System.Globalization
open Farkle
open Farkle.Builder
open Farkle.Builder.OperatorPrecedence
open Farkle.Parser

[<PrecompilerInput>]
let expressionWithOperators() =
    let number =
        Regex.regexString "[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?"
        |> terminal (T(fun _ input -> Double.Parse(input, CultureInfo.InvariantCulture)))

    let expression = nonterminal "Expression"
    setProductions expression [
        !@ expression .>> "+" .>>. expression => (fun x1 x2 -> x1 + x2)
        !@ expression .>> "-" .>>. expression => (fun x1 x2 -> x1 - x2)
        !@ expression .>> "*" .>>. expression => (fun x1 x2 -> x1 * x2)
        !@ expression .>> "/" .>>. expression => (fun x1 x2 -> x1 / x2)
        !@ expression .>> "^" .>>. expression => (fun x1 x2 -> Math.Pow(x1, x2))
        !& "-" .>>. expression |> prec NEG => (fun x -> -x)
        !& "(" .>>. expression .>> ")" |> asProduction
        !@ number |> asProduction]

    expression.WithOperatorScope(OperatorScope(
        new LeftAssociative("+", "-"),
        new LeftAssociative("*", "/"),
        new PrecedenceOnly(NEG),
        new RightAssociative("^")))

[<PrecompilerOutput>]
let parser() = CharParser.mustPrecompile<float>

printfn "This is a simple mathematical expression parser powered by Farkle."
printfn "Insert your expression and press enter."

let rec repl parser =
    match Console.ReadLine() with
    | null -> ()
    | input ->
        input
        |> CharParser.parseString parser
        |> printfn "%O"
        repl parser

repl <| parser()
```

---

> [!NOTE]
> You can learn more about the precompiler in its own [documentation page](the-precompiler.md).

## Conclusion

So, I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try, and maybe you have any question, found a bug, or want a feature, and want to [open a GitHub issue][githubIssues] as well. I hope that all of you have a wonderful day and to see you soon. Goodbye!

We have looked at several features of Farkle in this guide, but just scratched the surface. If you want to further explore what Farkle can do for you, here are some good next steps:

* [API reference](../api/index.md)
* [Customizing your grammar](./customizing-grammar.md) — how to customize your grammar by adding things like comments
* [Templating](./templates.md) — how to make a nice HTML page describing your grammar
* [Guide to resolving conflicts](./resolving-conflicts.md) — when things go wrong

[nuget]: https://www.nuget.org/packages/Farkle
[bnf]: https://en.wikipedia.org/wiki/Backus-Naur_form
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
