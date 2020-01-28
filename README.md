![Licensed under the MIT License](https://img.shields.io/github/license/teo-tsirpanis/farkle.svg)
[![NuGet](https://img.shields.io/nuget/v/Farkle.svg)](https://nuget.org/packages/Farkle)
[![.NET Build Status](https://img.shields.io/appveyor/ci/teo-tsirpanis/farkle/master.svg)](https://ci.appveyor.com/project/teo-tsirpanis/farkle)

# Farkle

<!--"Modern" is a marketing catchphrase, but keep in mind that FsLexYacc is definitely not "modern"-->
Farkle is a modern and easy-to-use parser library for F# and C#, that creates [LALR parsers][lalr] from composable [parser combinator][combinator]-like objects. Moreover, it can read grammars created by [GOLD Parser][gold] (the project that inspired Farkle) and provides an API to post-process them into arbitrary types.

## Documentation

* [Quick Start: Creating a calculator](https://teo-tsirpanis.github.io/Farkle/quickstart.html)
* [API Reference](https://teo-tsirpanis.github.io/Farkle/reference/index.html)

## Maintainer(s)

- [@teo-tsirpanis](https://github.com/teo-tsirpanis)

[lalr]:https://en.wikipedia.org/wiki/LALR_parser
[combinator]:https://en.wikipedia.org/wiki/Parser_combinator
[gold]:http://goldparser.org/
