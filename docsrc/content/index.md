# What is Farkle?

Farkle is a text parser library for F# and C#. It creates LALR grammars from composable objects that resemble parser combinators.

## Features

* __Speed__: Farkle is fast, taking advantage of .NET features such as spans and dynamic code generation.
* __Integration with MSBuild__: Farkle can optionally integrate with MSBuild to enable features like [ahead-of-time grammar building](the-precompiler.html), which drastically reduces startup times.
* __Large file support__: Farkle can parse large files very efficiently.
* __Free software__: Farkle is available under the MIT License.
* __Wide framework support__: Farkle targets .NET Standard 2.0, supporting .NET Framework 4.6.1+, .NET Core 2.0+, Xamarin, UWP and Unity.
<!-- * __Templating__: Farkle supports [creating templated text files from grammars](templating-reference.html) using [Scriban]. -->

[Learn more](choosing-a-parser.html) about Farkle's features, compared with its competition.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Farkle library can be <a href="https://nuget.org/packages/Farkle">installed from NuGet</a>:
      <pre>dotnet add package Farkle</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

## Documentation

The library comes with comprehensible documentation.

 * [Quick Start: Creating a calculator](quickstart.html) to get started with writing a simple calculator in F#.

 * [Using Farkle with C#](csharp.html) to learn what changes when using Farkle in a C# project.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules and functions in the library.

## Samples

* A __JSON parser__ powered by Farkle and written in both [C#][json-csharp] and [F#][json-fsharp].

* A __mathematical expression parser__ powered by Farkle and written in [F#][simplemaths], also showcasing some more advanced features.

## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork the project and submit pull requests.

The library is available under the MIT license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the [License file][license] in the GitHub repository.

  [scriban]: https://github.com/lunet-io/Scriban
  [json-csharp]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.CSharp/JSON.cs
  [json-fsharp]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/JSON.fs
  [simplemaths]: https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/SimpleMaths.fs
  [gh]: https://github.com/teo-tsirpanis/Farkle
  [issues]: https://github.com/teo-tsirpanis/Farkle/issues
  [license]: https://github.com/teo-tsirpanis/Farkle/blob/master/LICENSE.txt
