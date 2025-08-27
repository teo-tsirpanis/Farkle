# What is Farkle?

Farkle is a .NET library that helps you create text parsers. Most projects in this field are either _parser generators_ — which are faster and easier to debug, but require writing a grammar definition in a different language — or _parser combinator libraries_ — which are defined in source code and have a better development experience, but are slower and more error-prone. Farkle however combines both approaches: it uses the same algorithm as generated parsers, while allowing you to define your grammar in source code, providing performance, reliability and ease of use at the same time.

Farkle follows the paradigm introduced by [GOLD Parser][gold], which uses a binary file format to serialize grammars. This allows you to write code that introspects your grammars and use them for things like [rendering Scriban templates](./docs/templates.md), but also [precompile your grammars ahead of time](./docs/the-precompiler.md), providing high startup performance and compile-time error checking.

You can [learn more](docs/choosing-a-parser.md) about Farkle's features, compared with other .NET parsers.

## Quick start

Farkle can be [installed from NuGet][nuget]. Afterwards, you can proceed with writing your first parser:

# [C#](#tab/csharp)

```csharp
using Farkle;
using Farkle.Builder;

// Define a grammar for simple addition expressions
var number = Terminals.Int32("Number");
var addExpression = Nonterminal.Create("Add Expression",
    number.Extended().Append("+").Extend(number).Finish((n1, n2) => n1 + n2)
);

// Build the grammar and get a parser object
var parser = addExpression.Build();

// Parse a simple expression
var result = parser.Parse("1 + 2"); // Whitespace is ignored by default
Console.WriteLine(result); // Outputs: 3
```

# [F#](#tab/fsharp)

```fsharp
open Farkle
open Farkle.Builder

// Define a grammar for simple addition expressions
let number = Terminals.Int32("Number")
let addExpression = "Add Expression" ||= [
    !@ number .>> "+" .>>. number => (fun n1 n2 -> n1 + n2)
]

// Build the grammar and get a parser object
let parser = GrammarBuilder.build addExpression

// Parse a simple expression
let result = CharParser.parseString parser "1 + 2" // Whitespace is ignored by default
printfn "%O" result // Outputs: 3
```

---

## Documentation

The library comes with comprehensive documentation.

* [Tutorial: Creating a calculator](docs/quickstart.md) to get started with writing a calculator.

* [API Reference](api/index.md) contains automatically generated documentation for all types and functions in the library.

## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork the project and submit pull requests.

The library is available under the MIT license, which allows modification and redistribution for both commercial and non-commercial purposes. For more information see the [License file][license] in the GitHub repository.

[gold]: https://en.wikipedia.org/wiki/GOLD_(parser)
[nuget]: https://nuget.org/packages/Farkle
[gh]: https://github.com/teo-tsirpanis/Farkle
[issues]: https://github.com/teo-tsirpanis/Farkle/issues
[license]: https://github.com/teo-tsirpanis/Farkle/blob/mainstream/LICENSE.txt
