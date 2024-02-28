(**
---
category: Documentation
categoryindex: 1
index: 1
description: A quick start guide to Farkle.
---
*)
(*** hide ***)
#r "nuget: Farkle, 6.5.1"

(**
# Quick Start: Creating a calculator

Hello everyone. This guide will help you use Farkle. We will be using F#, but during the process, you will learn some useful things about Farkle itself. There's [another guide that explains what's different with C#][csharp]. Familiarity with context-free grammars and parsing will be very helpful.

## How Farkle works

While parser combinator libraries like FParsec combine many small parsers into a big parser, Farkle combines simple _grammars_ (i.e. descriptions of languages) into more complex ones, and has a single, multi-purpose parser. These composable grammars are called _designtime Farkles_ and implement the `Farkle.Builder.DesigntimeFarkle` interface.

Also, as with FParsec, a designtime Farkle can "return" something. In this case it also implements the `DesigntimeFarkle<TResult>` interface. For our calculator, our designtime Farkles will return a number, which is the numerical result of our mathematical expression.

> __Warning:__ Despite designtime Farkles being interfaces, implementing it in your code is not allowed and will throw an exception if a custom designtime Farkle implementation is somehow passed to Farkle.

To be able to use a designtime Farkle, we will first give it to a component called the _builder_. The builder will check the grammar for errors, create the necessary structures for the parser, and give us another special object called a _runtime Farkle_. With a runtime Farkle, we can parse text to our heart's desires, but in contrast with a designtime Farkle, we cannot compose it.

10: By the way, Farkle means "FArkle Recognizes Known Languages Easily".

20: And "FArkle" means (GOTO 10).

30: I guess you can't read this line.

## Designing our grammar

We want to design a grammar that represents mathematical expressions on floating-point numbers. The supported operations will be addition, subtraction, multiplication, division, and unary negation. The operator precedence has to be honored, as well as parentheses.

A similar grammar but on the integers, can be found [here][calculator]

For those that don't know, a context-free grammar is made of _terminals_, _nonterminals_ and _productions_.

* Terminals are elementary symbols that correspond to characters from our source text.

*
    Nonterminals are composite symbols made of terminals and other nonterminals.

    One of the nonterminals is designated as the _start symbol_, and it is the nonterminal from which parsing will start.

* Productions are the rules that define what symbols can be placed inside a nonterminal, and at which order.

### The terminals

We don't have to explicitly write a terminal for the mathematical symbols. They are just symbols that can have only one value and do not contain any meaningful information other than their presence. Farkle calls these types of symbols _literals_ and treats them specially to reduce boilerplate.

So we are now left with one terminal to define; the terminal for our decimal numbers. In Farkle, terminals are made of a regular expression that specifies the text that can match to this terminal, and a delegate called _transformer_ that converts the text to an arbitrary object.

There are three ways to create this terminal, starting from the simplest:

---

The `Farkle.Builder.Terminals` has functions that allow you to create some commonly needed terminals, like integers or floating-point numbers. We create our terminal this way:
*)

open Farkle
open Farkle.Builder
open System

let number = Terminals.genericReal<float> false "Number"

(**
The boolean parameter specifies whether to allow a minus sign at the beginning (we don't). The last parameter is the terminal's name, used for error reporting.

The `Terminals` module has more functions. You can see them all [in the documentation](reference/farkle-builder-terminals.html).

---

If Farkle doesn't have a ready to use function for your terminal, we have to create the terminal ourselves. The most easy way to do it is to write a regex using a string:
*)

let numberStringRegex =
    Regex.regexString @"\d+(\.\d+)?(e[+-]?\d+)?"
    |> terminal "Number" (T(fun _ x -> float (x.ToString())))

(**
The `regexString` function uses a quite familiar regex syntax. You can learn more about it [at its own documentation page][stringRegexes].

Let's take a look at the `terminal` function. Its last parameter is the regex, which we passed at the beggining for convenience and its first parameter is the terminal's name; nothing unusual here. Its second parameter is called a _transformer_ and is a delegate that converts the characters matched by our regex to an arbitrary object; in our case an integer.

#### Writing a transformer

The transformer's first parameter is an object of type [`ITransformerContext`](reference/farkle-itransformercontext.html) and is useful if you want to access the token's position or share some state between transformers, and its second parameter is a `ReadOnlySpan` of the characters matched by our regex, which will be converted to a floating-point number by our transformer.

`T` is the delegate's F# name. Because you have to specify it due to the language's limitations it was shortened to one letter for brevity.

You can view transformers as little parsers that always parse well-formatted text. In our example, the transformer for `Number` does not usually have to worry about exceptions from invalid input since the regex we specified guarantees the kind of input it expects. There are exceptions to this, like the user passing an extremely high integer. Such inputs will cause an exception to be raised. The built-in terminals the `Farkle.Builder.Terminals` module provides will handle the exception a little more gracefully but still fail the parsing.

Speaking of graceful errors, you can raise errors in your transformer by calling the `error` function, or the `errorf` function if you want formatted strings. If you are using C# or want to customize the exact position of the error, you can directly throw a `Farkle.ParserApplicationException`. They will be caught by the parser and can be uniformly handled like Farkle's own errors, as we will see later.

---

For the most advanced use cases, Farkle allows you to construct a regex from code. Directly constructing a regex from code is rarely useful for the average user of Farkle, but might come in handy when for example the regex's structure is not known at compile time, or it is complex enough to merit some code reuse.
*)

open Farkle.Builder.Regex

let numberConstructedRegex =
    // Regexes are composable!
    let atLeastOneNumber = chars PredefinedSets.Number |> atLeast 1
    // You can freely mix string regexes and constructed regexes.
    // let atLeastOneNumber = regexString @"\d+"
    concat [
        atLeastOneNumber
        optional <| (char '.' <&> atLeastOneNumber)
        [chars "eE"; chars "+-" |> optional; atLeastOneNumber]
        |> concat
        |> optional
    ]
    |> terminal "Number" (T(fun _ x -> float (x.ToString())))

(**
You can learn more about the functions above [at the documentation](reference/farkle-builder-regexmodule.html). More character sets of the `Farkle.Builder.PredefinedSets` module can also be found [at the documentation](reference/farkle-builder-predefinedsets.html)

> __Note:__ The regexes' type is `Farkle.Builder.Regex`. They are totally unrelated to .NET's `System.Text.RegularExpressions.Regex`. We can't convert between these two types, or directly match text against Farkle's regexes.

The terminal we created is of type `DesigntimeFarkle<float>`. This means that we can use it to parse floating-point numbers from text, but we want to create something bigger than that. As we are going to see, we can compose designtime Farkles into bigger ones, using nonterminals.

## The nonterminals.

### Writing simple nonterminals.

Because the calculator's nonterminals are a bit complicated, we have to take a brief interlude and tell how to create simpler ones.

Say we want to make a very simple calculator that can either add or subtract two numbers together. And let's say that an empty string would result to zero. This is the grammar of our calculator in [Backus-Naur Form][bnf]:

```
<Exp> ::= Number + Number
<Exp> ::= Number - Number
<Exp> ::= <>
```

For those that don't understand the snippet above, we define a nonterminal named `Exp`, that has three productions associated with it, meaning an `Exp` can be made in three ways: either by taking a sequence of the `Number` terminal, either the `+` or the `-` terminal, and another `Number` terminal, or by no symbols at all.

Writing the same thing in Farkle is actually surprisingly simple:
*)

let justTwoNumbers = "Exp" ||= [
    !@ number .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
    !@ number .>> "-" .>>. number => (fun x1 x2 -> x1 - x2)
    empty =% 0.0
]

(**
Let's explain what was going here. With the `||=` operator, we define a nonterminal with its productions. In its left side goes its name, and in its right side go the productions that can produce it.

See these strange symbols inside the list? They chain designtime Farkles together and signify which of them have information we care about. `!@` starts defining a production with its first member carrying significant information (the first operand). To start a production with a designtime Farkle that does not carry significant information, we can use `!%`.

The `.>>` and `.>>.` operators resemble FParsec's ones. `.>>` chains a new designtime Farkle we don't care what contains, and `.>>.` chains one we do.

With `.>>`, we can also chain string literals, instead of creating a terminal for each. We can also start a production with a literal using the `!&` operator.

The `=>` operator finishes the creation of a production with a function that combines its members that we marked as significant. Such functions are called _fusers_. In the first production we added the numbers and in the second we subtracted them. So, depending on the expression we entered, `_justTwoNumbers` would return either the sum, or the difference of them. Obviously, all productions of a nonterminal have to return the same type.

In the third case, we defined an empty production using `empty` (what a coincidence!) We used `empty =% 0.0` as a shortcut instead of writing `empty => (fun () -> 0.0)`.

An unfinished production is called a _production builder_. You can mark up to 16 significant members in a production builder.

You can pass an empty list in the right hand of the `||=` operator but the grammar will be invalid. A nonterminal must always have at least one production.

### Writing more complex nonterminals

We want our calculator to implement the following grammar [taken from Bison's documentation][bison-calculator]:

```
%left '-' '+'
%left '*' '/'
%precedence NEG
%right '^'

exp:
    NUM
    | exp '+' exp        { $$ = $1 + $3;      }
    | exp '-' exp        { $$ = $1 - $3;      }
    | exp '*' exp        { $$ = $1 * $3;      }
    | exp '/' exp        { $$ = $1 / $3;      }
    | '-' exp  %prec NEG { $$ = -$2;          }
    | exp '^' exp        { $$ = pow ($1, $3); }
    | '(' exp ')'        { $$ = $2;           }
;
```

And this is how to implement it in Farkle:
*)

open Farkle.Builder.OperatorPrecedence

let expression =
    let NEG = obj()

    let expression = nonterminal "Expression"
    expression.SetProductions(
        !@ number |> asIs,
        !@ expression .>> "+" .>>. expression => (fun x1 x2 -> x1 + x2),
        !@ expression .>> "-" .>>. expression => (fun x1 x2 -> x1 - x2),
        !@ expression .>> "*" .>>. expression => (fun x1 x2 -> x1 * x2),
        !@ expression .>> "/" .>>. expression => (fun x1 x2 -> x1 / x2),
        !& "-" .>>. expression |> prec NEG => (fun x -> -x),
        !@ expression .>> "^" .>>. expression => (fun x1 x2 -> Math.Pow(x1, x2)),
        // We use |> asIs instead of => (fun x -> x).
        !& "(" .>>. expression .>> ")" |> asIs
    )

    let opScope =
        OperatorScope(
            LeftAssociative("+", "-"),
            LeftAssociative("*", "/"),
            PrecedenceOnly(NEG),
            RightAssociative("^")
        )

    DesigntimeFarkle.withOperatorScope opScope expression

(**
As you see, our grammar in Farkle looks pretty similar to the one in Bison. Let's take a look at some newly introduced things:

### Defining recursive nonterminals

The `nonterminal` function is useful for recursive nonterminals. It creates a nonterminal whose productions will be set later with the `SetProductions` method. We can only once set them, all together. Calling the method again will have no effect.

Our nonterminal is of type `Nonterminal<float>`, but implements the `DesigntimeFarkle<float>` interface.

### Operator Precedence

We specify the operator associativity and precedence using an _operator scope_ that is made of _associativity groups_. There are four associativity group types: `LeftAssociative`, `RightAssociative`, `NonAssociative` and `PrecedenceOnly` that behave similarly to Bison's `%left`, `%right`, `%nonassoc` and `%precedence`. Their difference is best explained [at Bison's documentation][bison-assoc-types].

The groups in an operator scope are sorted by precedence in ascending order. In our grammar, the `+` and `-` symbols have the lowest precedence, followed by `*` and `/`. Until now, all these operators are left-associative, meaning that `1 + 2 + 3` is interpreted as `(1 + 2) + 3`.

Next in the precedence hierarchy is the unary negation. We can't define `-` again; instead we use the `prec` function to assign the unary negation's production a _contextual reflection token_, which is a dummy object that represents the production in the operator scope. This way, Farkle will recognize that `-` has higher precedence in unary negation than in subtraction. That new group is of type `PrecedenceOnly`, meaning that it doesn't specify associativity; only precedence.

And at the highest priority we have the exponentiation operator, which is right associative, meaning that `2 ^ 3 ^ 4` is interpreted as `2 ^ (3 ^ 4)`.

We set this operator scope to our designtime Farkle using the `DesigntimeFarkle.withOperatorScope` function. There are some more things to be careful about when using operator scopes:

* Setting a second operator scope to a designtime Farkle will discard the one that was previous set.

* An operator can belong in only one operator scope. Otherwise undefined behavior will occur.

## Building our grammar

With our nonterminals being ready, it's time to create a runtime Farkle that can parse mathematical expressions. The builder will create tables for the parser using the LALR algorithm, and a Deterministic Finite Automaton (DFA) for the tokenizer. It also creates a special object called a _post-processor_ that is responsible for executing the transformers and fusers.

All that stuff can be done with a single line of code:
*)

let myMarvelousRuntimeFarkle = RuntimeFarkle.build expression

(**
## Using the runtime Farkle

Now that we got it, it's time to put it to action. Farkle supports parsing text from various sources, namely strings, arbitrary character buffers on the heap (like substrings, arrays or parts of arrays) using `System.ReadOnlyMemory<char>`, files and `System.IO.TextReader`s.

The functions return an F# `Result` type whose error value (if it unfortunately exists), can show exactly what did go wrong.

> __Note:__ If a grammar is invalid (has an LALR conflict, two terminals are indistinguishable or something else), building would still succeed, but parsing would fail every time.

Let's look at some some examples:
*)

open System.IO

// You can consume the parsing result like this:
match RuntimeFarkle.parseString myMarvelousRuntimeFarkle "103 + 137+281" with
| Ok result -> printfn "The answer is %f" result
// The %O format specifier (or alternatively, calling ToString())
// will create human-readable error messages.
| Error err -> printfn "Error: %O" err

// You can parse any Memory<char>, such a substring or even an array of characters!
let mem = "The answer is 45".AsMemory().Slice(14)
RuntimeFarkle.parseMemory myMarvelousRuntimeFarkle mem

RuntimeFarkle.parseFile myMarvelousRuntimeFarkle "example.txt"

let myStringReader = new StringReader("45 + 198 - 647 + 2 * 478 - 488 + 801 - 248")
RuntimeFarkle.parseTextReader myMarvelousRuntimeFarkle myStringReader

(**
## Customizing our designtime Farkle

Before we finish, let's take a look at one more thing; how to further customize a designtime Farkle.

* Most programming languages have comments, so why would Farkle not support them as well? We can create a designtime Farkle that adds support for comments in another one. Both block and line comments are supported. They cannot be nested.

* By default, grammars are not case sensitive. But we can make them if we want.

* By default, whitespace characters are ignored. We can change it once again.

* We can also specify a symbol that will be discarded when encountered by the parser. These symbols are called _noise symbols_ and are defined by regexes.

We will see some customizations as an example:
*)

let _customized =
    expression
    // You can add as many types of block or line comments as you want.
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.caseSensitive true
    // Whether to ignore whitespace between terminals; true by default.
    |> DesigntimeFarkle.autoWhitespace false
    // Adds an arbitrary symbol that will be ignored by Farkle.
    // It needs a regex, and a name for diagnostics purposes.
    |> DesigntimeFarkle.addNoiseSymbol "Letters" (chars AllLetters)

(**
> __Note:__ These customizations have to be done at the top-level designtime Farkle that is going to be built (or they will have no effect) and always apply to the entire grammar.

---

So, I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try, and maybe you have any question, found a bug, or want a feature, and want to [open a GitHub issue][githubIssues] as well. I hope that all of you have a wonderful day and to see you soon. Goodbye!

[csharp]: csharp.html
[calculator]: https://github.com/teo-tsirpanis/Farkle/blob/2ecc66d6b7b43a1b52b889aec78e865c0c5cf325/sample/Farkle.JSON.FSharp/SimpleMaths.fs#L68
[predefinedSets]: http://goldparser.org/doc/grammars/predefined-sets.htm
[stringRegexes]: string-regexes.html
[bnf]: https://en.wikipedia.org/wiki/Backus-Naur_form
[bison-calculator]: https://www.gnu.org/software/bison/manual/html_node/Infix-Calc.html
[bison-assoc-types]: https://www.gnu.org/software/bison/manual/html_node/Using-Precedence.html
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
*)
