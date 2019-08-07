(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Farkle/net45/"
#load "../../packages/formatting/FSharp.Formatting/FSharp.formatting.fsx"
#r "Farkle.dll"

(**
# Quick Start: Creating a calculator

This guide will help you to start using Farkle. It is assumed that you are using F# as your development language.

## Installing GOLD Parser Builder

First, you need to install the GOLD Parser Builder. You can grab it from [here][goldBuilder].

> __Note:__ You might run into some issues with GOLD's site. A way to mitigate them is to explicitly write `http://` at the beginning of the site. If you still cannot download them for any reason, I have made [a mirror in MEGA][mega].

GOLD Parser Builder is a .NET Framework application. If you don't run Windows, I think it will work fine on Mono.

## Writing a grammar for the calculator

Now it's a good time to learn [how to write GOLD grammars][writingGrammars].

So, let's write our own simple grammar for this tutorial. Open GOLD Parser Builder and write [this sample grammar][sampleGrammar].

## Preparing Farkle.

It's also time to install Farkle's NuGet package with [your favorite NuGet client][paket] (or another one).

You also need to add the package named [Farkle.Tools.MSBuild][farkleToolsMSBuildNuGet]. This gives us some additional design-time support that we will need later.

## Compiling the grammar

Let's get back to GOLD Parser:

![The GOLD Parser Builder](img/goldBuilder.png)

See the button writing "Next" at the bottom-right? Keep pressing it until you see this dialog.

![The save dialog](img/saveAsEGT.png)

Pay attention to the file type. Only EGT files work.

Save this file next to the other source files of your project, open the project file and modify it by adding the following lines before your source files as shown:

``` xml
<ItemGroup>
  <Farkle Include="SimpleMaths.egt" Namespace="MyBeautifulCalculator" />
  <Compile Include="SimpleMaths.egt.fs" />
  <!--The rest of your source code files.-->
</ItemGroup>
```

Let's look at the second line. It tells MSBuild - the build system our projects are powered up by - that we are going to use this grammar file from Farkle. We even gave it a custom namespace of our own - probably to match the name of our app.

Now, on to the second line. We are adding a new source file with an unusual extension to our project. But, where is it actually? Wouldn't the compiler refuse to compile our little project and raise a nasty compiler error? It's actually surprisingly simple. Thanks to previous line, MSBuild generated a new source file which contains our beloved grammar, with some types to make using Farkle really easy. Let's take a look at this file:
*)

// This file was created by Farkle.Tools version 5.0.0 at 2019-05-12.
// It should NOT be committed to source control.
// namespace ``MyBeautifulCalculator``.Definitions
// EDIT: We can't declare a namespace in a documentation file.

/// A terminal of the MyBeautifulCalculator language.
type Terminal =
    /// '-'
    | Minus = 3u
    /// '('
    | LParen = 4u
    /// ')'
    | RParen = 5u
    /// '*'
    | Times = 6u
    /// '/'
    | Div = 7u
    /// '+'
    | Plus = 8u
    /// Number
    | Number = 9u

/// A production of the MyBeautifulCalculator language.
type Production =
    /// <Expression> ::= <Add Exp>
    | Expression = 0u
    /// <Add Exp> ::= <Add Exp> '+' <Mult Exp>
    | AddExpPlus = 1u
    /// <Add Exp> ::= <Add Exp> '-' <Mult Exp>
    | AddExpMinus = 2u
    /// <Add Exp> ::= <Mult Exp>
    | AddExp = 3u
    /// <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
    | MultExpTimes = 4u
    /// <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
    | MultExpDiv = 5u
    /// <Mult Exp> ::= <Negate Exp>
    | MultExp = 6u
    /// <Negate Exp> ::= '-' <Value>
    | NegateExpMinus = 7u
    /// <Negate Exp> ::= <Value>
    | NegateExp = 8u
    /// <Value> ::= Number
    | ValueNumber = 9u
    /// <Value> ::= '(' <Expression> ')'
    | ValueLParenRParen = 10u

[<RequireQualifiedAccess>]
module Grammar =
    /// The grammar of MyBeautifulCalculator, encoded in Base64.
    let asBase64 = "[Too big to fit here :-p]"

(**
This file contains enumeration types to represent each possible terminal and production that may appear in our grammar, but also, our EGT encoded in Base-64. This way, we don't have to carry it around in a separate file. Hooray!

> __Note:__ As you have seen at the beginning of this generated source file, this file our hard-working build system generated does not need to be tracked by source control. It can just be generated when it's time to build. Moreover, if we change the EGT file, it gets generated again, and if we run `dotnet clean`, it gets deleted. We should however keep the EGT file, and the GOLD parser grammar in text form (to make it easier to change), because Farkle does not make its own EGT files. Yet.

## Making a post-processor

To calculate the value of a mathematical expression, we will need to make a post-processor.

A post-processor converts the syntax tree of an expression of a language into anything we want. In this case, it converts it down to a single integer which is the result of the math operation. A post-processor is made of `Transformer`s and `Fuser`s.

### Making the transformers

First, we have to say what a transformer is. A transformer is a special object that converts a terminal symbol of a type to any object you want.

For example, let's say that in our grammar we have a terminal of type `Number` with value `"478"`. We want to convert this string of digits to an integer. Therefore a transformer for `Number`s will just convert this string to an integer.

For this grammar actually, `Number` is the only terminal we care about. There are others like `+`, `-` and so on, but we don't care about them because they can only take one value.

Having said that, it's time to create a file that will house our heroic post-processor, whose only transformer will be the following:
*)

open Farkle
open Farkle.PostProcessor
open System

let transformers = [
    Transformer.createS Terminal.Number Int32.Parse
]

(**
Let's take a look at the definition of our transformer. `Transformer.createS Terminal.Number Int32.Parse` creates a transformer that transforms the characters of the terminals of type `Number` into a string, and immediately, converts this string into an integer.

> __Note:__ As you might have noticed, each time a `Number` gets transformed, Farkle creates a string which is immediately discarded, after its conversion to an integer. If you are parsing larger grammars and want to really minimize allocations like this, there are more advanced methods to create a transformer, [which you can see in the documentation][transformerDocumentation].

Don't worry about the terminals that are missing from the list. They are automatically transformed into `null`.

### Making the fusers

Now, let's see what a fuser is. A fuser is another special object that combines the parts of a production into one object.

For example, we have the following production: `<Add Exp> ::= <Add Exp> '+' <Mult Exp>` (also known as `Productions.AddExpPlus`). This production is made of three parts: an expression, the "plus" character and another expression. The plus character's value is always `null`, because we did not declare a transformer for this terminal. We want to take the first and the last parts and add them together. So we have the following fuser:
*)

let myFuser = Fuser.take2Of Production.AddExpPlus (0, 2) (+)

(**
Here we take the zeroth and the second parts - because as we all know, arrays start at zero -, and add them together.

You might wonder: how can we "add" expressions as if they were integers? It's actually surprisingly simple. Our magical post-processor will make them _actual_ integers. But you will understand it better when we complete the fusers:
*)

open Fuser // This will make our declarations shorter.

let fusers =
    [
        identity Production.Expression // identity means that we just take the first item of a production, as it is.
        myFuser // We have already written this fuser before.
        take2Of Production.AddExpMinus (0, 2) (-)
        identity Production.AddExp
        take2Of Production.MultExpTimes (0, 2) (*)
        take2Of Production.MultExpDiv (0, 2) (/)
        identity Production.MultExp
        take1Of Production.NegateExpMinus 1 (~-) // (~-) is different than (-). The tilde denotes an unary operator.
        identity Production.NegateExp
        identity Production.ValueNumber
        take1Of Production.ValueLParenRParen 1 id
    ]

(**
As you see, the fuser turns the production `<Value> ::= Number` into an integer, by taking the `Number`, which is an integer, because the transformer made it so. The `<Negate Exp>` becomes an integer as well, because in both its definitions, it takes a `<Value>` which either negates it, or takes it as it is. By following this logic, we fill find out that every production becomes an integer. Hooray!

As a sidenote, if we forget to add a fuser that is needed by the post-processor, it will raise an error that specifically tells us which fuser we forgot.

With the transformers and fusers ready, we now create the post-processor like this. See that we specified the type of final objects it produces.
*)

let pp = PostProcessor.ofSeq<int> transformers fusers

(**
> __Note__: The post-processor is not perfectly type-safe. I could have used a function that returns something else other than an integer, and the compiler would not shed a tear at all. However, the library will catch this error and will not throw an exception into your code.

## Making a runtime Farkle

With our post-processor ready, we need to make a `RuntimeFarkle`. This object is responsible for parsing our beautiful mathematical expressions and post-process them into an integer

10: By the way, Farkle means: "FArkle Recognizes Known Languages Easily".

20: And "FArkle" means: (GOTO 10).

30: I guess you can't read this line.

A runtime Farkle is made of a grammar, and a post-processor. We have already created the post-processor, and the grammar is in a Base64-encoded string, so we can create it this way:
*)

let myMarvelousRuntimeFarkle = RuntimeFarkle.ofBase64String pp Grammar.asBase64

(**
### Using the runtime Farkle

Now that we got it, it's time to use it. The `RuntimeFarkle` module has many functions to parse text from different sources. There is a simple function called `parse` which just parses a string. If you want to log what the parser actually does, you can use the function `parseString`. 

Actually, all functions except `parse` take a function as a parameter, that gets called for every parser event. You can see what these events look like [here][parseMessageDocumentation].

Furthermore, all functions return an F# `Result` type whose error value (if it unfortunately exists), can show exactly what did go wrong.

Let's look at some some examples:
*)

open System.IO
open System.Text

/// A utility to print a parse result.
let printIt (x: Result<_,FarkleError>) =
    match x with
    | Ok x -> printfn "%d" x
    // The following line pretty-prints the error message in English.
    | Error x -> printfn "%O" x

RuntimeFarkle.parse myMarvelousRuntimeFarkle "45 + 198 - 647 + 2 * 478 - 488 + 801 - 248"
|> printIt

RuntimeFarkle.parseString myMarvelousRuntimeFarkle Console.WriteLine "111*555"
|> printIt

RuntimeFarkle.parseFile myMarvelousRuntimeFarkle ignore Encoding.ASCII "gf.m"
|> printIt

File.OpenRead "fish.es"
|> RuntimeFarkle.parseStream myMarvelousRuntimeFarkle ignore false Encoding.UTF8
|> printIt // The third parameter, shows whether to lazily load the file into memory.
// Here, false means to read it all at once.

RuntimeFarkle.parseFile myMarvelousRuntimeFarkle Console.WriteLine Encoding.Unicode "math.txt"
|> printIt

(**
## Bonus: Using Farkle.Tools

What if I told you, that there is a way to make this entire process much simpler? Since Version 5.0, Farkle has a set of command-line tools to help you make beautiful parsers. They are [distributed over NuGet][farkleToolsNuGet], and you can install them by running this little command:

`dotnet tool install -g Farkle.Tools`

Let's go back now, right after we added the EGT file into our project. Now, open your favorite command line shell, and type the following lines:

`farkle new -t postprocessor SimpleMaths.egt`

You will see a file named `SimpleMaths.fs` that contains most of the post-processor code we have already written. What you have to do, is to fix the compiler errors by completing your own transformers and fusers, and you are ready to go!

---

So that's it. I hope you understand. If you have any question, found a bug, or want a feature, feel free to [open a GitHub issue][githubIssues].

[goldBuilder]: http://goldparser.org/builder/index.htm
[mega]: https://mega.nz/#F!opp3yToY!FMRD5CxS-q_-SN8f5TAbrA
[farkleToolsMSBuildNuGet]: https://www.nuget.org/packages/Farkle.Tools.MSBuild
[writingGrammars]: http://goldparser.org/doc/grammars/index.htm
[sampleGrammar]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Calculator/SimpleMaths.grm
[paket]: https://fsprojects.github.io/Paket/
[transformerDocumentation]: reference/farkle-postprocessor-transformermodule.html
[parseMessageDocumentation]: reference/farkle-parser-parsemessage.html
[farkleToolsNuGet]: https://www.nuget.org/packages/Farkle.Tools
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
*)
