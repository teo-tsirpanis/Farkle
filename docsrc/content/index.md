# What is Farkle?

Farkle is a parser generator for F# and other .NET languages. It is made (or _will_ be made) of the following components:

* An engine for the [GOLD Parsing system][gold].
* A code generator that creates type-safe Abstract Syntax Trees (under development, _only_ for F#).
* MsBuild Integration (future plan).
* A replacement of GOLD Parser Builder (I am not sure about it).
* A Farkle type provider (I am not sure about it too).

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Farkle library can be <a href="https://nuget.org/packages/Farkle">installed from NuGet</a>:
      <pre>PM> Install-Package Farkle</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>


## Documentation

The library comes with comprehensible documentation. 

 * [Quick Start](quickstart.html) to get started.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules and functions in the library. This includes additional brief samples on using most of the functions.

## Some important notes

### 3rd-party bundled libraries

Farkle is also bundled with some F# libraries that uses. They are imported as [Paket GitHub dependencies][paket-github] and you can use them without installing another NuGet package. These libraries are:

  * [Aether], an open-source optics library.

  * [FSharpx.Collections], a library with efficient immutable collection types. __⚠ Only `RandomAccesList` is bundled from FSharpx.Collections__

The second library is not available for .NET Standard. I have filed [an issue](https://github.com/fsprojects/FSharpx.Collections/issues/77) to support .NET Standard, yet it appears that the project is no longer maintained.

These libraries __may be removed from the project in any time if they are not needed anymore.__

### Respecting SemVer

Farkle is a new library with a growing and developing API. Old designs are replaced with newer ones many times. Moreover, it also contains some code not related with its purpose, but used internally. Its purpose is to make parsing text easily.

[Semantic Versioning][semver] is making things harder, and version numbers growing faster. Even worse, taking SemVer into consideration while changing APIs that have no connection with parsing text, makes the project harder to be changed.

In a 🌰🐚: __APIs other than some high-level ones (like `GOLDParser` and `EGT`) are subject to breaking changes even between minor releases. You use them at your own risk.__
 
## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation.

The library is available under the MIT license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/teo-tsirpanis/Farkle/tree/master/docs/content
  [gh]: https://github.com/teo-tsirpanis/Farkle
  [paket-github]: https://fsprojects.github.io/Paket/github-dependencies.html
  [aether]: https://xyncro.tech/aether/
  [fsharpx.collections]: http://fsprojects.github.io/FSharpx.Collections/index.html
  [semver]: http://semver.org/
  [issues]: https://github.com/teo-tsirpanis/Farkle/issues
  [license]: https://github.com/teo-tsirpanis/Farkle/blob/master/LICENSE.txt
  [gold]: http://www.goldparser.org/
