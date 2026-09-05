![Licensed under the MIT License](https://img.shields.io/github/license/teo-tsirpanis/farkle.svg)
[![NuGet](https://img.shields.io/nuget/v/Farkle.svg)](https://nuget.org/packages/Farkle)
[![CI](https://github.com/teo-tsirpanis/Farkle/actions/workflows/ci.yml/badge.svg)](https://github.com/teo-tsirpanis/Farkle/actions/workflows/ci.yml)
[![CII Best Practices](https://bestpractices.coreinfrastructure.org/projects/5005/badge)](https://bestpractices.coreinfrastructure.org/projects/5005)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/teo-tsirpanis/Farkle/badge)](https://api.securityscorecards.dev/projects/github.com/teo-tsirpanis/Farkle)
[![Discord Server](https://badgen.net/discord/members/mYzXu5Zt8J)](https://discord.gg/mYzXu5Zt8J)

# Farkle

Farkle is a .NET library for building fast, reliable text parsers in C# and F#. It creates [LALR(1)][lalr] and [IELR(1)][ielr] parsers through an API that resembles [parser combinators][combinator], combining generated-parser performance and reliability, with a source-code-first development experience.

Farkle follows the [GOLD Parser][gold] approach of serializing grammars to a binary format. This makes grammars available for introspection, and enables features such as [Scriban template rendering](https://farkle.dev/templates.html), and [ahead-of-time precompilation](https://farkle.dev/the-precompiler.html) for faster startup and compile-time grammar validation.

## Quick Start

Install [Farkle from NuGet](https://www.nuget.org/packages/Farkle), then define and build a grammar in code:

### C#

```csharp
using Farkle;
using Farkle.Builder;

var number = Terminals.Int32("Number");
var addExpression = Nonterminal.Create("Add Expression",
	number.Extended().Append("+").Extend(number).Finish((n1, n2) => n1 + n2)
);

var parser = addExpression.Build();
var result = parser.Parse("1 + 2"); // Whitespace is ignored by default.
Console.WriteLine(result); // 3
```

### F#

```fsharp
open Farkle
open Farkle.Builder

let number = Terminals.Int32("Number")
let addExpression = "Add Expression" ||= [
	!@ number .>> "+" .>>. number => (fun n1 n2 -> n1 + n2)
]

let parser = GrammarBuilder.build addExpression
let result = CharParser.parseString parser "1 + 2" // Whitespace is ignored by default.
printfn "%O" result // 3
```

## Documentation

* [Creating a calculator](https://farkle.dev/quickstart.html)
* [Choosing a parser](https://farkle.dev/choosing-a-parser.html)
* [Migrating to Farkle 7](https://farkle.dev/migration/60-70.html)
* [Precompiler reference](https://farkle.dev/the-precompiler.html)
* [API reference](https://farkle.dev/api/index.html)

## Contributing

Issues and pull requests are welcome at [teo-tsirpanis/Farkle](https://github.com/teo-tsirpanis/Farkle). See the [contribution guide](CONTRIBUTING.md) for development and submission guidance.

## Maintainer(s)

- [@teo-tsirpanis](https://github.com/teo-tsirpanis)

[lalr]:https://en.wikipedia.org/wiki/LALR_parser
[ielr]:https://www.sciencedirect.com/science/article/pii/S0167642309001191
[combinator]:https://en.wikipedia.org/wiki/Parser_combinator
[gold]:https://en.wikipedia.org/wiki/GOLD_(parser)
