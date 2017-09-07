(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Farkle/net462/"
#load "../../packages/build/FSharp.Formatting/FSharp.formatting.fsx"

(**
# Quick Start

This guide will help you to start using Farkle.

## Installing GOLD Parser Builder

First, you need to install the GOLD Parser Builder. You can grab it from [here][goldBuilder].

__Note:__ You might run into some issues with GOLD's site. A way to mitigate them is to explicitly write `http://` at the beginning of the site. If you still cannot download them for any reason, I have made a mirror in [Google Drive][drive].

GOLD Parser Builder is a .NET Framework application. If you don't run Windows, you can run it on Mono.

Now it's a good time to learn [how to write GOLD grammars][writingGrammars].

But we will write our own simple grammar for this tutorial. Open GOLD Parser Builder and write this:

```
"Name"     = 'Enter the name of the grammar'
"Author"   = 'Enter your name'
"Version"  = 'The version of the grammar and/or language'
"About"    = 'A short description of the grammar'

"Start Symbol" = <Program>

! -------------------------------------------------
! Character Sets
! -------------------------------------------------


! -------------------------------------------------
! Terminals
! -------------------------------------------------

Identifier = {Number}+

! -------------------------------------------------
! Rules
! -------------------------------------------------

! The grammar starts below
<Program> ::= <Expression>

<Expression>  ::= <Expression> '>'  <Add Exp> 
               |  <Expression> '<'  <Add Exp> 
               |  <Expression> '<=' <Add Exp> 
               |  <Expression> '>=' <Add Exp>
               |  <Expression> '==' <Add Exp>    !Equal
               |  <Expression> '<>' <Add Exp>    !Not equal
               |  <Add Exp> 

<Add Exp>     ::= <Add Exp> '+' <Mult Exp>
               |  <Add Exp> '-' <Mult Exp>
               |  <Mult Exp> 

<Mult Exp>    ::= <Mult Exp> '*' <Negate Exp> 
               |  <Mult Exp> '/' <Negate Exp> 
               |  <Negate Exp> 

<Negate Exp>  ::= '-' <Value> 
               |  <Value> 

!Add more values to the rule below - as needed

<Value>       ::= Identifier
               |  '(' <Expression> ')'
```

![The GOLD Parser Builder](img/goldBuilder.png)

See the button writing "Next" at the bottom-right? ~Mash~ Keep pressing it until you see this dialog.

![The save dialog](img/saveasegt.png)

Pay attention to the file type. Only EGT files work.

## Preparing Farkle.

After you install the NuGet package with [your favorite NuGet client][paket] (or another one), write these lines to your source file:
*)

#r "Chessie.dll" // You only need it on FSI.
#r "Farkle.dll" // You only need it on FSI.

open Farkle
open Farkle.Parser // The high-level API resides here.

(**
## Creating a parser object

Now, we must create a `GOLDParser` object. This object is the easiest way to parse a string. And it is created like this:
*)

let parser = GOLDParser("simple.egt") // We pass the grammar's file name. Note that if there is a problem with the file, an exception will be raised.

let parser2 = GOLDParser("simple.egt", true) // The second parameter indicates whether the resulting parse tree is simplified. It's false by default.

(**
We want to parse a simple mathematical expression like one of these:

`111 * 555 == 61605`

`477 == 0`

`617 == (1 / 0)`

`617 > 198 > 45 > 477`

> __Note:__ We don't _evaluate_ the expression. We just make it in a form that a computer can easier understand.

We can parse data from many sources like these:
*)

let result = parser.ParseString "477 = 0" // A simple string.

let result2 = parser.ParseFile "WhatAWonderful.file" // A file.

let result3 = System.IO.File.OpenRead "ThisWasA.triumph" |> parser.ParseStream // Or a stream. This is almost the same thing with `ParseFile`.

(**
See the result's type? It's a `ParseResult`. It is a simple discriminated union that represents either a parsing success or a parsing failure

In other words, if the functions succeed, they give a `Reduction`, which is a structure that describes a parse tree.

It also carries a list of "messages" which are like the log of a parser.

If the parsing fails, the message that explains the failure is separated from the rest..

The `ParseMessage` is made a discriminated union that documents every possible error (the "impossibles" (like stream errors) throw exceptions 😛), and the position it happened.

- But these errors are not so descriptive for the end user. Isn't there a way to make them more simpler?

- Yes it is. ParseResult has some utility functions which make our life easier! 😃

We can do this for example:
*)

result.MessagesAsString |> Seq.iter (printfn "%s")
match result.Simple with
| Choice1Of2 x ->
    printfn "Success!"
    x |> Reduction.drawReductionTree |> printfn "%s" // drawReductionTree makes a fancy ASCII parse tree from a reduction.
| Choice2Of2 x -> printfn "Oops! There is an error, sorry: %s" x

(**
Now you should see something like this in your console:

```
Success!
+--<Program> ::= <Expression>
|  +--<Expression> ::= <Expression> '==' <Add Exp>
|  |  +--<Expression> ::= <Add Exp>
|  |  |  +--<Add Exp> ::= <Mult Exp>
|  |  |  |  +--<Mult Exp> ::= <Negate Exp>
|  |  |  |  |  +--<Negate Exp> ::= <Value>
|  |  |  |  |  |  +--<Value> ::= 'Identifier'
|  |  |  |  |  |  |  +--477
|  |  +--==
|  |  +--<Add Exp> ::= <Mult Exp>
|  |  |  +--<Mult Exp> ::= <Negate Exp>
|  |  |  |  +--<Negate Exp> ::= <Value>
|  |  |  |  |  +--<Value> ::= 'Identifier'
|  |  |  |  |  |  +--0
```
*)

(**
I hope you understand. If you have any question, found a 🐛, or want a feature, feel free to [open a GitHub issue][githubIssues].
I didn't tell you how to navigate through a `Reduction` yet. The Reduction type is a little hard to use, but a dedicated `AST` type will enter the stage in the future.

[goldBuilder]: http://goldparser.org/builder/index.htm
[drive]: https://drive.google.com/open?id=0BxWFaQD-qcKlOFEweUZWdUtadnM
[writingGrammars]: http://goldparser.org/doc/grammars/index.htm
[paket]: https://fsprojects.github.io/Paket/
[githubIssues]: https://github.com/teo-tsirpanis/farkle/issues
*)
