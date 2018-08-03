(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Farkle/net472/"
#load "../../packages/build/FSharp.Formatting/FSharp.formatting.fsx"
#r "Farkle.dll"

(**
# Quick Start: Creating a calculator

This guide will help you to start using Farkle. It is assumed that you are using F# as your development language.

## Installing GOLD Parser Builder

First, you need to install the GOLD Parser Builder. You can grab it from [here][goldBuilder].

> __Note:__ You might run into some issues with GOLD's site. A way to mitigate them is to explicitly write `http://` at the beginning of the site. If you still cannot download them for any reason, I have made [a mirror in MEGA][mega].

GOLD Parser Builder is a .NET Framework application. If you don't run Windows, I think it will work fine on Mono.

GOLD Parser Builder also has a feature to create skeleton programs; templates that help you write code to use the grammar you have written. I have preapred [a template for use with F# and Farkle][template], but we will not use it in the tutorial in order to better understand the code behind the parser. If you want, download it to the installation folder of GOLD Parser Builder, in the `Templates` subfolder (on Windows it is `C:\Program Files (x86)\GOLD Parser Builder\Templates`).

> I know it might sound like a lot of work, but at a future release, Farkle will work independently of GOLD Parser Builder.

## Writing a grammar for the calculator

Now it's a good time to learn [how to write GOLD grammars][writingGrammars].

So, let's write our own simple grammar for this tutorial. Open GOLD Parser Builder and write [this sample grammar][sampleGrammar].

## Preparing Farkle.

It's also time to install Farkle's NuGet package with [your favorite NuGet client][paket] (or another one).

## Compiling the grammar

![The GOLD Parser Builder](img/goldBuilder.png)

See the button writing "Next" at the bottom-right? <s>Mash</s> Keep pressing it until you see this dialog.

![The save dialog](img/saveasegt.png)

Pay attention to the file type. Only EGT files work.

Now, click on the menu bar: `Project - Create Skeleton Program`.

You will be presented with the following dialog.

![Create a Skeleton Program... dialog](img/createSkeletonProgram.png)

Save it somewhere where the rest of your code files will be.

## Writing the parser

The skeleton program starts with some very useful types. Copy the following snippet to a file.
*)

open Farkle
open Farkle.Grammar
open Farkle.PostProcessor

type Symbol =
/// (EOF)
| EOF        =  0
/// (Error)
| Error      =  1
/// Whitespace
| Whitespace =  2
/// '-'
| Minus      =  3
/// '('
| LParen     =  4
/// ')'
| RParen     =  5
/// '*'
| Times      =  6
/// '/'
| Div        =  7
/// '+'
| Plus       =  8
/// Number
| Number     =  9
/// <Add Exp>
| AddExp     = 10
/// <Expression>
| Expression = 11
/// <Mult Exp>
| MultExp    = 12
/// <Negate Exp>
| NegateExp  = 13
/// <Value>
| Value      = 14

type Production =
/// <Expression> ::= <Add Exp>
| Expression        =  0
/// <Add Exp> ::= <Add Exp> '+' <Mult Exp>
| AddExpPlus        =  1
/// <Add Exp> ::= <Add Exp> '-' <Mult Exp>
| AddExpMinus       =  2
/// <Add Exp> ::= <Mult Exp>
| AddExp            =  3
/// <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
| MultExpTimes      =  4
/// <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
| MultExpDiv        =  5
/// <Mult Exp> ::= <Negate Exp>
| MultExp           =  6
/// <Negate Exp> ::= '-' <Value>
| NegateExpMinus    =  7
/// <Negate Exp> ::= <Value>
| NegateExp         =  8
/// <Value> ::= Number
| ValueNumber       =  9
/// <Value> ::= '(' <Expression> ')'
| ValueLParenRParen = 10

(**
As you see, the skeleton program has generated an enumeration type for each symbol and production of the grammar. They are also automatically commented.

## Making a post-processor

To calculate the value of a mathematical expression, we will need to make a post-processor.

A post-processor converts the syntax tree of an expression of a language into anything we want. In this case, it converts it down to a single integer which is the result of the math operation. A post-processor is made of `Transformer`s and `Fuser`s.

### Making the transformers

First, we have to see what a transformer is. A transformer is a special object that converts a terminal symbol of a type to any object you want.

For example, let's say that in our grammar we have a terminal of type `Number` with value `"478"`. We want to convert this string of digits to an integer. Therefore a transformer for `Number`s will just call `Convert.ToInt32` to this string.

For this grammar actually, `Number` is the only terminal we care about. There are others like `+`, `-` and so on, but we don't care about them because they offer no useful information for us _yet_. Other symbols like `EOF`, `Error` and `Expression` might exist in the enum type just because the source file is generated by a tool. Even if there is a transformer for them, it will not actually transform anything. You can even delete them from the type definition if you want.

So, the transformers of our grammar will be the following:
*)

let transformers = [
    Symbol.Number, Transformer.create System.Convert.ToInt32
]

(**
Don't wory about the terminals that are missing from the list. They are automatically ignored.

### Making the fusers

Now, let's see what a fuser is. A fuser is another special object that combines the parts of a production into one object.

For example, we have the following rule: `<Add Exp> ::= <Add Exp> '+' <Mult Exp>` (also known as `Rules.AddExpPlus`). This rule is made of three parts: an expression, the "plus" character and another expression. We want to take the first and the last parts and add them together. So we have the following fuser:
*)

let myFuser = Production.AddExpPlus, Fuser.take2Of (0, 2) 3 (+) // We actually take the zeroth and the second parts, but as we all know, arrays start at zero.

(**
You might wonder: how can we "add" expressions as if they were integers? It will turn out to be that the post-processor will make them _actual_ integers! But you will understand it better when we complete the fusers:
*)

open Fuser // Open this module so that we don't have to prepend the Fuser module every time.

let fusers =
    [
        Production.Expression       , identity // identity means that the production is made of one part and we just take it as it is.
        myFuser // We have already written this fuser before.
        Production.AddExpMinus      , take2Of (0, 2) 3 (-)
        Production.AddExp           , identity
        Production.MultExpTimes     , take2Of (0, 2) 3 (*)
        Production.MultExpDiv       , take2Of (0, 2) 3 (/)
        Production.MultExp          , identity
        Production.NegateExpMinus   , take1Of 1 2 (~-) // (~-) is different than (-). The tilde denotes an unary operator.
        Production.NegateExp        , identity
        Production.ValueNumber      , identity
        Production.ValueLParenRParen, take1Of 1 3 id
    ]

(**
As you see, the fuser turns the production `<Value> ::= Number` into an integer, by taking the `Number`, which is an integer, because the transformer made it so. The `<Negate Exp>` becomes an integer as well, because in both its definitions, it takes a `<Value>` which either negates it, or takes it as it is. By following this logic, we fill find out that every production becomes an integer.

And here comes the post-processor...
*)

let ThePostProcessor = PostProcessor.ofSeq transformers fusers

(**
> __Note__: The post-processor is not _yet_ perfectly type-safe. I could use a function that returns something else other than an integer, and the compiler would not shed a tear at all. However, the library will catch this error and will not throw an exception. The post-processor will be made type-safe at a later release.

## Making a runtime Farkle

We need to make a `RuntimeFarkle`. This object is responsible for parsing __and post-processing__ a string, a file, a .NET stream, or a [`HybridStream<char>`](/reference/farkle-hybridstream.html), which is a custom type made for Farkle.

10: By the way, Farkle means: "FArkle Recognizes Known Languages Easily".
20: And "FArkle" means: (GOTO 10) 😁
30: I guess you can't read this line. 😛

A runtime Farkle is made of a grammar, a post-processor, and two functions to convert terminals and productions to our custom enum types.

We have the first two already, so let's make the last two:
*)

let fSymbol =
    function
    /// Terminal symbols have indices that correspond to those at the grammar, and to the values of the symbol enum.
    | Terminal (index, _) -> index |> int |> enum<Symbol>
    /// For type-safety, symbols other than terminals are converted to an impossible enum.
    /// Even if the transformer stumbles upon something other than a terminal, he will be forced to ignore them.
    /// Wait! Nonterminals have symbols as well!
    /// Yeah, but it's very unlikely that the transformer will ever encounter one of them.
    | _ -> enum -1

let fProduction x  =
    x
    |> Indexable.index // Farkle's productions implement the `Indexable` interface. They always have always indices, unlike symbols.
    |> int
    |> enum<Production>

(**
Now it's time for the big 🧀
*)

let TheRuntimeFarkle = RuntimeFarkle<int>.CreateFromFile "SimpleMaths.egt" fSymbol fProduction ThePostProcessor

(**
### Using the runtime Farkle

Now that we got it, it's time to use it. Let's see some examples:
*)

open Farkle.Parser

/// A utility to print a parse result.
let printIt (x: Result<_,FarkleError>) =
    x
    |> tee (sprintf "%d") string // Tee is a Farkle library function.
    |> printfn "%s"

TheRuntimeFarkle.ParseString "45 + 198 - 647 + 2 * 478 - 488 + 801 - 248" |> printIt // A string

TheRuntimeFarkle.ParseFile "goblins_from.mars" |> printIt // A file

System.IO.File.OpenRead "fishe.s" |> TheRuntimeFarkle.ParseStream |> printIt // A stream

TheRuntimeFarkle.ParseFile("itsjustgettingwetttonig.ht",GOLDParserConfig.Default.WithEncoding(System.Text.Encoding.UTF7).WithLazyLoad(false)) |> printIt // Another file, but with UTF-7 encoding and eagerly loaded.

let parseResult, log = "111*555" |> HybridStream.ofSeq false |> TheRuntimeFarkle.ParseChars // ParseChars also returns a log of the parser.
log |> List.iter (printfn "%O")
printIt parseResult

(**
> __Note__: It has been observed that the first time a runtime Farkle parses something takes significantly more time than the rest. This happens because it reads the EGT file the first time. There is no workaround for it available. But it's only the first time.

So that's it. I hope you understand. If you have any question, found a 🐛, or want a feature, feel free to [open a GitHub issue][githubIssues].
*)

(**
[goldBuilder]: http://goldparser.org/builder/index.htm
[mega]: https://mega.nz/#F!opp3yToY!FMRD5CxS-q_-SN8f5TAbrA
[writingGrammars]: http://goldparser.org/doc/grammars/index.htm
[sampleGrammar]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Calculator/SimpleMaths.grm
[template]: https://github.com/teo-tsirpanis/Farkle/blob/master/src/Farkle/F-Sharp%20-%20Farkle.pgt
[paket]: https://fsprojects.github.io/Paket/
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
*)