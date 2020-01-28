(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Farkle/net45/"
#load "../../packages/formatting/FSharp.Formatting/FSharp.formatting.fsx"
#r "System.Memory.dll"
#r "Farkle.dll"

(**
# Quick Start: Creating a calculator


Hello everyone. This guide will help you use Farkle. We will be using F#, but during the process, you will learn some useful things about Farkle itself. There's [another guide that explains what's different with C#][csharp]. You also have to be familiar with context-free grammars and parsing.

## How Farkle works

> __Note__: This is an oversimplified, high-level description of Farkle. I will write a more technical description of it in the future.

While parser combinator libraries like FParsec combine many small parsers into a big parser, Farkle combines simple _grammars_ (i.e. descriptions of languages) into more complex ones, and has a single, multi-purpose parser. These composable grammars are called _designtime Farkles_.

Also, as with FParsec, a designtime Farkle can "return" something. To accomplish this, there is a special object called a _post-proessor_. Post-processors create a domain-specific type from the input text. For our calculator, we will automatically create a post-processor that returns a number, which is the numerical result of our mathematical expression.

To be able to use a designtime Farkle, we will first feed it to a component called the _builder_. The builder will check the grammar for errors, create parsing tables with the LALR algoritm, and give us another special object called a _runtime Farkle_. Runtime Farkles contain a grammar and a post-processor. With a runtime Farkle, we can parse text to our heart's desires.

10: By the way, Farkle means: "FArkle Recognizes Known Languages Easily".

20: And "FArkle" means: (GOTO 10).

30: I guess you can't read this line.

## Designing our grammar

We want to design a grammar that represents a mathematical expressions on floating-point numbers. The supported operations will be addition, subtraction, multiplication, division, and unary negation. The operator precedence has to be honored, as well as parentheses.

A similar grammar but on the integers, can be found [here][calculator]

### The terminals

This grammar will have seven terminals: the number, and each of the math symbols, and the parentheses. In Farkle, we create terminals from hard-coded regular expressions (regexes). Let's see how a regular expression that recognizes a deimal number looks like:
*)

open Farkle
open Farkle.Builder
open Farkle.Builder.Regex
open System

let numberRegex =
    // Regexes are composable!
    let atLeastOneNumber = chars Number |> atLeast 1
    concat [
        atLeastOneNumber
        optional <| (char '.' <&> atLeastOneNumber)
        [chars "eE"; chars "+-" |> optional; atLeastOneNumber]
        |> concat
        |> optional
    ]

(**
`Number` is an object implementing `IEnumerable<char>` which is called a _predefined set_. [A list of all predefined sets][predefinedSets] can be found at the documentation of GOLD Parser, the project that inspired Farkle.

> __Note:__ A future release will allow creating regexes from strings.

With our regex being ready, we will create the terminal this way:
*)

let number = terminal "Number" (T(fun _ x -> Double.Parse(x.ToString()))) numberRegex

// Farkle also gives us this, but we can't
// use it because we only want positive numbers.
let _number = Terminals.float "Number"

(**
The function `terminal` takes three arguments. The first is the name of the terminal. The name is a purely informative field; many terminals with the same name are allowed. The second argument is called a _transformer_. It's a delegate that gets the position of the terminal and a `ReadOnlySpan<char>` that contains the terminal's data. The third argument is the regex that recognizes it. As we saw in `_number`, there are many more sample terminals in the [`Terminals` module](reference/farkle-builder-terminals.html). 

> __Note:__ The regexes' type is `Farkle.Builder.Regex`. They are totally unrelated to `System.Text.ResularExpressions.Regex`. We can't convert between these two types, or directly match text against Farkle's regexes.

The terminal we created is of type `DesigntimeFarkle<float>`. This means that we can theoretically use it to parse floating-point numbers from text. But we want to create someting bugger than that. As we are going to see, we can compose designtime Farkles into bigger ones, using nonterminals.

Also, before we continue, we don't have to explicitly write a terminal for the mathematical symbols. These are just symbols that do not return anything meaningful. We call them _literals_. Literals in Farkle are specially treated to reduce boilerplate.

## The nonterminals.

### Writing simple nonterminals.

Because the calculator's nonterminals are a bit complicated, we have to take a brief interlude and tell how to create simpler ones.

Say we want to make a very simple calculator that can either add or subtract two numbers together. And let's say that an empty string would result to zero. Writing this is actually surprisingly simple:
*)

let _justTwoNumbers = "Exp" ||= [
    !@ number .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
    !@ number .>> "-" .>>. number => (fun x1 x2 -> x1 - x2)
    empty =% 0.
]

(**
Let's explain what was going here. With the `||=` operator, we define a nonterminal. In its left side goes its name, and in its right side go the productions that produce it. In the above code, we defined these two productions:

```
<Exp> ::= Number + Number
<Exp> ::= Number - Number
<Exp> ::= <>
```

See these strange symbols? They chain designtime Farkles together and signify which of them carry information we care about. `!@` starts defining a production with its first member carrying significant information (the first operand). To start a production with a designtime Farkle that does not carry significant information, we can use `!%`. The `.>>` and `.>>.` operators resemble FParsec's ones. `.>>` chains a new designtime Farkle we don't care what contains, and `.>>.` chains one we do.

With `.>>`, we can also chain string literals, instead of creating a terminal for each.

> __Note:__ `!@ x` is essentially equivalent to `empty .>>. x`.

> __Note:__ To start a production with a literal, use the `!&` operator.

The `=>` operator finishes a production with a function that decides what to do with its members that we marked as significant. In the first case, we added the numbers, and in the second, we subtracted them. So, depending on the text, `_justTwoNumbers` would return either the sum, or the difference of them. Obviously, all productions of a nonterminal have to return the same type.

In the third case, we defined an empty production using `empty` (what a coincidence!) We used `empty =% 0.` instead of writing `empty => (fun () -> 0.)`.

> __Note:__ An unfinished production is called a _production builder_. You can mark up to 16 significant members in a production builder.

> __Note:__ You can't pass an empty list in the right hand of the `||=` operator, or the grammar will be invalid.

### Writing more complex nonterminals

Now, our complete calculator grammar would look like this:

```
// That 's the starting symbol
<Expression> ::= <Add Exp>

<Add Exp> ::= <Add Exp> '+' <Mult Exp>
<Add Exp> ::= <Add Exp> '-' <Mult Exp>
<Add Exp> ::= <Mult Exp>

<Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
<Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
<Mult Exp> ::= <Negate Exp>

<Negate Exp> ::= '-' <Value>
<Negate Exp> ::= <Value>

<Value> ::= Number
<Value> ::= '(' <Expression> ')'
```

> __Note:__ We hardcoded associativity and operator precedence to make the grammar unambiguous. A future release will allow configuring them more intuitively.

The problem with this grammar is that the definitions of all nonterminals form a circle. And designtime Farkles are immutable, like almost everything else with F#. We can solve this problem however and the solution is actually surprisingly simple. But it's better to see how it is done in code:
*)

let addExp, multExp, negateExp, value =
    nonterminal "Add Exp", nonterminal "Mult Exp",
    nonterminal "Negate Exp", nonterminal "Value"

let expression = "Expression" ||= [
    !@ addExp => id
]

addExp.SetProductions(
    !@ addExp .>> "+" .>>. multExp => (+),
    !@ addExp .>> "-" .>>. multExp => (-),
    !@ multExp => id
)

multExp.SetProductions(
    !@ multExp .>> "*" .>>. negateExp => (*),
    !@ multExp .>> "/" .>>. negateExp => (/),
    !@ negateExp => id
)

negateExp.SetProductions(
    !& "-" .>>. value => (~-),
    !@ value => id
)

value.SetProductions(
    !@ number => id,
    !& "(" .>>. expression .>>  ")" => id
)

(**
The magic lies within the `nonterminal` function. With this function, we create a nonterminal with a name, but we can set its production _later_ with the `SetProductions` method. We can only once set them, all together. Calling the method again will be ignored.

The nonterminals are of type `Nonterminal<float>`, but implement `DesigntimeFarkle<float>`; it's actually an interface.

> __Warning:__ Despite designtime Farkles being interfaces, implementing it on your code is not allowed and will throw an exception if used.

## Building our grammar

With our nonterminals being ready, it's time to create a runtime Farkle that can parse mathematical expressions. The builder uses the LALR algorithm to create parser tables for our parser. It also creates a post-processor that will handle the parsed numbers.

> __Note:__ If a grammar is invalid (has an LALR conflict, two terminals are indistinguishable or something else), building would still succeed, but parsing would fail every time.
*)

let myMarvelousRuntimeFarkle = RuntimeFarkle.build expression

(**
## Using the runtime Farkle

Now that we got it, it's time to put it to action. The `RuntimeFarkle` module has many functions to parse text from different sources. There is a simple function called `parse` which just parses a string. If you want to log what the parser actually does, you can use the function `parseString`. 

Actually, all functions except `parse` take a function as a parameter, that gets called for every parser event. You can see what these events look like [here][parseMessageDocumentation].

The functions return an F# `Result` type whose error value (if it unfortunately exists), can show exactly what did go wrong.

Let's look at some some examples:
*)

open System.IO

// You can consume the parsing result like this:
match RuntimeFarkle.parse myMarvelousRuntimeFarkle "103 + 137+281" with
| Ok result -> printfn "The answer is %f" result
| Error err -> eprintfn "Error: %O" err

RuntimeFarkle.parse myMarvelousRuntimeFarkle "45 + 198 - 647 + 2 * 478 - 488 + 801 - 248"

RuntimeFarkle.parseString myMarvelousRuntimeFarkle (printfn "%O") "111*555"

// You can parse any Memory<char>, such a substring or even an array of characters!
let mem = "The answer is 45".AsMemory().Slice(14)
RuntimeFarkle.parseMemory myMarvelousRuntimeFarkle ignore mem

RuntimeFarkle.parseFile myMarvelousRuntimeFarkle ignore "gf.m"

RuntimeFarkle.parseTextReader myMarvelousRuntimeFarkle ignore (File.OpenText "fish.es")

(**
## Bonus: Customizing our designtime Farkle

Before we continue, it would be nice to see how we can customize a designtime Farkle.

Most programming languages have comments, so why would Farkle not support them as well? We can create a designtime Farkle that adds support for comments in another one. Both block and line comments are supported. They cannot be nested.

By default, grammars are not case sensitive. But we can customize this behavior.

By default, whitespace characters is ignored. We can customize it once again.

We can also specify a symbol that will be discarded when encountered by the parser. These symbols are called _noise symbols_.

We will see some customizations as an example:

> __Warning:__ These customizations have to be done at the designtime Farkle that is going to be built (or they will have no effect) and always apply to the entire grammar.
*)

let _customized =
    expression
    // You can add as many types of block or line comments as you want.
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> DesigntimeFarkle.addLineComment "//"
    // Obviously, only the last change matters.
    |> DesigntimeFarkle.caseSensitive true
    |> DesigntimeFarkle.autoWhitespace false
    |> DesigntimeFarkle.addNoiseSymbol "Letters" (chars AllLetters)

(**
---

So that's it. I hope you understand. If you have any question, found a bug, or want a feature, feel free to [open a GitHub issue][githubIssues].

[csharp]: csharp.html
[calculator]: https://github.com/teo-tsirpanis/Farkle/blob/2ecc66d6b7b43a1b52b889aec78e865c0c5cf325/sample/Farkle.JSON.FSharp/SimpleMaths.fs#L68
[predefinedSets]: http://goldparser.org/doc/grammars/predefined-sets.htm
[parseMessageDocumentation]: reference/farkle-parser-parsemessage.html
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
*)
