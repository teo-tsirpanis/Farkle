# Farkle.Tools.MSBuild

This package provides integration with MSBuild for the Farkle parser library.

## Precompiling grammars

The main feature of this integration is the ability to precompile grammars at build time. This offers multiple benefits, including better startup performance and compile-time grammar validation.

### C#

```csharp
using Farkle;
using Farkle.Builder;

var parser = GetParser();
var result = parser.Parse("1 + 2"); // Whitespace is ignored by default.
Console.WriteLine(result); // 3

[PrecompilerInput]
static IGrammarBuilder<int> GetGrammar()
{
    var number = Terminals.Int32("Number");
    return Nonterminal.Create("Addition",
        number.Extended().Append("+").Extend(number).Finish((left, right) => left + right)
    );
}

[PrecompilerOutput]
static CharParser<int> GetParser() => CharParser.MustPrecompile<int>();
```

### F#

```fsharp
open Farkle
open Farkle.Builder

[<PrecompilerInput>]
let getGrammar() =
    let number = Terminals.Int32("Number")
    "Addition" ||= [
        !@ number .>> "+" .>>. number => (fun left right -> left + right)
    ]

[<PrecompilerOutput>]
let getParser() = CharParser.mustPrecompile<int>

let parser = getParser()
let result = CharParser.parseString parser "1 + 2" // Whitespace is ignored by default.
printfn "%O" result // 3
```

## Generating HTML documentation

You can generate HTML documentation for your precompiled grammars by enabling the `FarkleGenerateHtml` property in your project:

```xml
<PropertyGroup>
  <FarkleGenerateHtml>true</FarkleGenerateHtml>
</PropertyGroup>
```

## Documentation

* [Precompiler reference](https://farkle.dev/the-precompiler.html)
