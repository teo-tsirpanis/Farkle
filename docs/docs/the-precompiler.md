# Precompiling your grammars to increase performance

Most bottom-up parsing tools are parser generators, and generate the grammar's parsing logic ahead of time when the application is built. Farkle however is a library, and does not normally have the ability to run at build time. This leads to increased startup times, as the grammar needs to be built every time the application starts. This leads to wasted time, especially because most grammars are fixed, and do not change between runs of the application.

The precompiler was created to bridge this gap. When you build your project, it runs the compiled assembly to get your grammars, builds them, and weaves the assembly to include them, so that it doesn't have to build them at runtime anymore. Besides improving startup times, precompiling your grammars has more advantages:

* You get build-time validation of your grammars, allowing you to catch errors early.
* You get additional features like [HTML documentation generation](#generating-html-documentation), and [Hot Reload integration](#hot-reload-integration).
* You can trim large parts of Farkle's builder code, reducing the size of your application.

## Prerequisites

In order to use the precompiler, you need to install the [`Farkle.Tools.MSBuild`][nuget] NuGet package to your project.

The precompiler supports all target frameworks and .NET SDK versions that Farkle supports, and is expected to work on IDEs that are using the .NET SDK under the hood. However, Visual Studio 2022 and earlier versions are not supported. If you are using Visual Studio to build projects using the precompiler, you have to use Visual Studio 2026 or later.

## Using the precompiler

Let's assume that you have a grammar defined like this:

# [C#](#tab/csharp)

```csharp
using Farkle;
using Farkle.Builder;

IGrammarSymbol<int> number = Terminals.Int32("Number");

IGrammarSymbol<int> expression = Nonterminal.Create("Expression",
    number.Extended().Append("+").Extend(number).Finish((x1, x2) => x1 + x2));

CharParser<int> parser = expression.Build();

Console.WriteLine("This is a simple mathematical expression parser powered by Farkle.");
Console.WriteLine("Insert your expression and press enter.");

while (Console.ReadLine() is { } input)
{
    Console.WriteLine(parser.Parse(input));
}
```

# [F#](#tab/fsharp)

```fsharp
open Farkle
open Farkle.Builder

let number = Terminals.int32 "Number"

let expression = "Expression" ||= [
    !@ number .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
    !@ number |> asProduction
]

let parser = GrammarBuilder.build expression

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

As explained [in the quick start guide](quickstart.md#bonus-precompiling-the-grammar), you can prepare your grammar to be precompiled by following these steps:

1. Move your grammar definition code to a separate static method (or F# top-level function) that returns @"Farkle.Builder.IGrammarBuilder" (also known as the _precompiler input method_).
   * And mark it with @"Farkle.Builder.PrecompilerInputAttribute".
2. Create a static method (or F# top-level function) that returns @"Farkle.CharParser`1" (also known as the _precompiler output method_).
   * And mark it with @"Farkle.Builder.PrecompilerOutputAttribute".
3. Update your code to call the precompiler output method to get the parser.

Here's how the above example would look like after applying these steps:

# [C#](#tab/csharp)

```csharp
using Farkle;
using Farkle.Builder;

CharParser<int> parser = CreateParser();

Console.WriteLine("This is a simple mathematical expression parser powered by Farkle.");
Console.WriteLine("Insert your expression and press enter.");

while (Console.ReadLine() is { } input)
{
    Console.WriteLine(parser.Parse(input));
}

[PrecompilerInput]
IGrammarBuilder<int> CreateGrammar()
{
    IGrammarSymbol<int> number = Terminals.Int32("Number");

    IGrammarSymbol<int> expression = Nonterminal.Create("Expression",
        number.Extended().Append("+").Extend(number).Finish((x1, x2) => x1 + x2));

    return expression;
}

[PrecompilerOutput]
CharParser<int> CreateParser() => CharParser.MustPrecompile<int>();
```

# [F#](#tab/fsharp)

```fsharp
open Farkle
open Farkle.Builder

[<PrecompilerInput>]
let createGrammar() =
    let number = Terminals.int32 "Number"

    "Expression" ||= [
        !@ number .>> "+" .>>. number => (fun x1 x2 -> x1 + x2)
        !@ number |> asProduction
    ]

[<PrecompilerOutput>]
let createParser() = CharParser.mustPrecompile<int>

let parser = createParser()

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

### Factory method requirements

Precompiler input and output methods must be static methods (or F# top-level functions) that accept no parameters. They must not be generic, or declared in a generic type.

Precompiler input methods must return @"Farkle.Builder.IGrammarBuilder", or a type that implements that interface, such as @"Farkle.Builder.IGrammarBuilder`1".

Precompiler ouptut methods must return one of the following types:

* @"Farkle.Grammars.Grammar"
* If the corresponding input method returns @"Farkle.Builder.IGrammarBuilder`1":
  * @"Farkle.CharParser`1". The type argument of the grammar builder must be assignable to the type argument of the parser.
* If the output method builds a [syntax-checking parser](#creating-syntax-checking-parsers):
  * @"Farkle.CharParser`1". The type argument of the parser must be a reference type.

Precompiler output methods are advised to call @"Farkle.CharParser.MustPrecompile``1?displayProperty=nameWithType"

### Defining multiple grammars in the same type

If you want to define multiple grammars in the same type, you have to set the `Key` property in both the @"Farkle.Builder.PrecompilerInputAttribute" and the @"Farkle.Builder.PrecompilerOutputAttribute" definitions, so that the precompiler can know which output method corresponds to which input method. Here's an example:

# [C#](#tab/csharp)

```csharp
public static class MyGrammars
{
    [PrecompilerInput(Key = "Grammar1")]
    public static IGrammarBuilder<int> CreateGrammar1() { ... }

    [PrecompilerOutput(Key = "Grammar1")]
    public static CharParser<int> CreateParser1() => CharParser.MustPrecompile<int>();

    [PrecompilerInput(Key = "Grammar2")]
    public static IGrammarBuilder<string> CreateGrammar2() { ... }

    [PrecompilerOutput(Key = "Grammar2")]
    public static CharParser<string> CreateParser2() => CharParser.MustPrecompile<string>();
}
```

# [F#](#tab/fsharp)

```fsharp
module MyGrammars =
    [<PrecompilerInput(Key = "Grammar1")>]
    let createGrammar1() = ...

    [<PrecompilerOutput(Key = "Grammar1")>]
    let createParser1() = CharParser.mustPrecompile<int>

    [<PrecompilerInput(Key = "Grammar2")>]
    let createGrammar2() = ...

    [<PrecompilerOutput(Key = "Grammar2")>]
    let createParser2() = CharParser.mustPrecompile<string>
```

---

Keys are case sensitive. Each input method in a type must have a unique key, and up to one input method in a type can have no key. Keys across different types do not have to be unique.

### Creating syntax-checking parsers

The precompiler supports creating parsers that only perform syntax checking, and always return `null` as the result object. A precompiler output method will return a syntax-checking parser if one of the following conditions is met:

* The corresponding input method does not return a type that implements @"Farkle.Builder.IGrammarBuilder`1".
* The @"Farkle.Builder.PrecompilerOutputAttribute.SyntaxCheck?displayProperty=nameWithType" property is set to `true`.

### LR conflict reporting

Errors during the grammar building process are reported as build errors of your project. By default, if the grammar has LR conflicts, the precompiler will generate an HTML page containing the grammar's LR state machine and conflicting states, and will emit a single build error pointing to that page.

You can make each conflict to be reported individually and toggle creating a conflict report, by setting the `FarklePrecompilerErrorMode` property in your project file to `ErrorsOnly`, `ReportOnly` (the default), or `Both`:

```xml
<PropertyGroup>
    <FarklePrecompilerErrorMode>Both</FarklePrecompilerErrorMode>
</PropertyGroup>
```

### Generating HTML documentation

The precompiler can generate an HTML page for your grammars at build time. To enable this, set the `FarkleGenerateHtml` property to `true` in your project file:

```xml
<PropertyGroup>
  <FarkleGenerateHtml>true</FarkleGenerateHtml>
</PropertyGroup>
```

The grammars will be placed in your project's output directory, and will be named after your [grammar's name](xref:Farkle.Grammars.GrammarInfo.Name). You can use the [WithGrammarName](xref:Farkle.Builder.GrammarBuilderExtensions.WithGrammarName(Farkle.Builder.IGrammarBuilder,System.String)) family of extension methods to change your grammar's name.

### Hot Reload integration

Farkle automatically adds Hot Reload support to @"Farkle.CharParser`1" objects returned by precompiler output methods. If you edit the type that defines a precompiled grammar, the grammar held by the parser object will be discarded, and will be rebuilt the next time the parser object gets used.

While Hot Reload integration cannot be disabled, it poses no overhead when [Hot Reload is not supported by the runtime](xref:System.Reflection.Metadata.MetadataUpdater.IsSupported), like in published applications.

## Additional considerations

### Code execution caveats

The precompiler takes the unusual step of _executing_ your project's code (specifically, the precompiler input methods) at build time. This adds some considerations and limitations when using the precompiler:

* You must ensure that your project's source code is trusted. This goes beyond the existing [MSBuild security guidelines](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-security-best-practices).
* It goes without saying that the precompiler input methods must be deterministic, and not have any side effects.
* The precompiler will execute your code in the context of your build environment. This means that if you are precompiling grammars in say a mobile app project, the precompiler might fail to execute your code if it depends on platform-specific APIs. In this case, you are recommended to move your grammar definitions to a separate cross-platform class library project.

### Dependency resolution

Your project's dependencies can be generally used by the precompiler input methods, with the following known limitations:

* Native library dependencies will not be resolved.
* RID-specific dependencies will not be resolved.
* Satellite assemblies will not be resolved.
* Using the precompiler will automatically set the [`CopyLocalLockFileAssemblies`](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#copylocallockfileassemblies) project property to `true`. If your project explicitly sets it to `false`, the precompiler might fail because of missing dependencies.

### Library version compatibility

The precompiler will build your grammars using the version of the Farkle library that is referenced by your project. The library is usually obtained through the `Farkle` NuGet package, but obtaining it through a project reference of a local build of Farkle, or by embedding Farkle's sources in your project is also supported.

The `Farkle.Tools.MSBuild` package uses a special internal interface to interact with the `Farkle` library. This interface may change in incompatible ways at any time. If you get an error about this, make sure that the versions of `Farkle` and `Farkle.Tools.MSBuild` match.

### Assembly unloadability

Objects returned by precompiler ouput methods may hold references to the assemblies that define them, preventing their respective assembly load contexts from being [unloaded](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability).

[nuget]: https://www.nuget.org/packages/Farkle.Tools.MSBuild/
