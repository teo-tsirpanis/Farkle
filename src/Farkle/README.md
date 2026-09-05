# Farkle

Farkle is a .NET library for building fast, reliable text parsers in F# and C#. It creates LALR(1) and IELR(1) parsers through an API that resembles parser combinators, letting you define grammars directly in source code without requiring a separate grammar definition language.

> [!TIP]
> Farkle can also generate parsing tables at build time, providing fast startup performance on par with generated parsers, and compile-time grammar validation. Check out the [`Farkle.Tools.MSBuild`](https://www.nuget.org/packages/Farkle.Tools.MSBuild) package for more information.

## Quick Start

### C#

```csharp
using Farkle;
using Farkle.Builder;

var number = Terminals.Int32("Number");
var expression = Nonterminal.Create("Addition",
    number.Extended().Append("+").Extend(number).Finish((left, right) => left + right)
);

var parser = expression.Build();
var result = parser.Parse("1 + 2"); // Whitespace is ignored by default.
Console.WriteLine(result); // 3
```

### F#

```fsharp
open Farkle
open Farkle.Builder

let number = Terminals.Int32("Number")
let expression = "Addition" ||= [
    !@ number .>> "+" .>>. number => (fun left right -> left + right)
]

let parser = GrammarBuilder.build expression
let result = CharParser.parseString parser "1 + 2" // Whitespace is ignored by default.
printfn "%O" result // 3
```

## Documentation

* [Create a calculator](https://farkle.dev/quickstart.html)
* [Choose the right parser](https://farkle.dev/choosing-a-parser.html)
* [Read the API reference](https://farkle.dev/api/index.html)
* [Browse the source and report issues](https://github.com/teo-tsirpanis/Farkle)
