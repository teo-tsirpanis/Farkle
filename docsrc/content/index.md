# What is Farkle?

Farkle is a text parser library for .NET, featuring the best of both worlds: __LALR parser combinators__. Users define the terminals and nonterminals of their grammars, what to do when each of them is encountered, and Farkle takes care of the rest.

It can be [installed from NuGet][nuget].

## Features

* __Speed__: Farkle is fast. Its performance is a top priority and lots of time has been invested in its optimization.
* __Language compatibility__: Farkle is usable from both C# and F#, with an intuitive API for each language. __It even supports C# 8.0's nullable reference types!__
* __Fast development cycle__: Unlike parser generators, Farkle does not generate any source files. Your grammars are type-safe and created entirely from code, allowing features like IntelliSense and real-time syntax error reporting.
* __Integration with MSBuild__: Farkle can optionally integrate with MSBuild to enable features like [ahead-of-time grammar building](the-precompiler.html) which drastically reduces startup times and catches grammar errors like LALR conflicts at compile time.
* __Large file support__: Farkle can parse large files without entirely reading them to memory.
* __Grammar introspection__: Farkle provides [APIs that allow your grammars to be inspected from code](reference/farkle-grammar-grammar.html).
* __Wide framework support__: Farkle targets .NET Standard 2.0, supporting .NET Framework 4.6.1+, .NET Core 2.0+, Xamarin, UWP and Unity.
<!-- * __Templating__: Farkle supports [creating templated text files from grammars](templating-reference.html) using [Scriban]. -->

[Learn more](choosing-a-parser.html) about Farkle's features, compared with other .NET parsers.

## Documentation

The library comes with comprehensible documentation.

 * [Quick Start: Creating a calculator](quickstart.html) to get started with writing a simple calculator in F#.

 * [Using Farkle with C#](csharp.html) to learn what changes when using Farkle in a C# project.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules and functions in the library.

## Samples

* A __JSON parser__ powered by Farkle and written in both [C#][json-csharp] and [F#][json-fsharp].

* A __mathematical expression parser__ powered by Farkle and written in [F#][simple-maths], also showcasing some more advanced features.

* A simple indent-based language parser that supports virtual terminals written in [F#][indent-based]. The most advanced example; it explains how to write a custom tokenizer.

## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork the project and submit pull requests.

The library is available under the MIT license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the [License file][license] in the GitHub repository.

  [nuget]: https://nuget.org/packages/Farkle
  [scriban]: https://github.com/lunet-io/Scriban
  [json-csharp]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.CSharp/JSON.cs
  [json-fsharp]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/JSON.fs
  [simple-maths]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/SimpleMaths.fs
  [indent-based]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/IndentBased.fs
  [gh]: https://github.com/teo-tsirpanis/Farkle
  [issues]: https://github.com/teo-tsirpanis/Farkle/issues
  [license]: https://github.com/teo-tsirpanis/Farkle/blob/master/LICENSE.txt
