# Farkle 7's F# API

As described in [another design document](csharp-rewrite.md), Farkle 7 will move from an F# library with an additional C# API, to a C# library with an additional F# API. This document describes the new F# API.

## Goals

* Provide an idiomatic F# API for Farkle 7. Idiomatic means:
    * Currying functions (and argument order that makes sense for currying).
    * camelCase for function names.
    * F#-friendly types (`Result<T,obj>` instead of `ParserResult<T>` and `unit` in syntax checkers).
        * After reconsideration, the F# parser APIs will keep returning `ParserResult<T>`, but we will provide a conversion function to F# results.
* Provide an easy way to migrate from Farkle 6's F# API. Most compile errors should be resolved by renames.
* Provide limited deprecation warnings for some of Farkle 6's legacy APIs.
* Fix mistakes in the Farkle 6 F# API.

## Non-goals

* Provide F# wrappings for every single functionality of the C# API.

## Scope

As with Farkle 6, Farkle 7's F# API will cover the parser and builder APIs, almost in their entirety, while the grammars API will only get F#-friendly functions to _create_ grammars.

## Distribution

There are three possible ways to distribute the F# API, each with its advantages and disadvantages:

### Option 1: Write the F# API in C# by directly using `FSharp.Core` in the `Farkle` assembly

F# gets compiled to regular IL bytecode, just like C#. By decompiling F# assemblies, we can observe the generated IL and write the equivalent C# code. Certain F# constructs like modules, curried functions and differing source and compiled names are encoded under the hood with custom attributes from the `FSharp.Core` assembly. We could use these attributes in the main `Farkle` assembly to write classes that the F# compiler would pass as written in F#.

#### Advantages

* Simple deployment: everything is in one assembly.
* Allows working around F# codegen inefficiencies, like https://github.com/fsharp/fslang-suggestions/issues/1083
    * That issue is not a big deal if left unresolved, and could be worked around with some reflection, like Farkle 6 does.

#### Disadvantages

* The `Farkle` package will have a mandatory dependency to `FSharp.Core`, which is a big package (2.5MB).
    * Size-conscious C# apps would be able to entirely trim it away on deployment, but not everyone can trim their apps and downloading the package in the first place cannot be prevented.
* Not all of F#'s constructs can be expressed in C#. Some like type aliases and SRTPs are encoded in special binary blobs embedded in the assembly. Manually writing a custom blob is not an option.

### Option 2: Bundle the F# API in a source file within the `Farkle` package

The F# API would be provided in one or more F# source files, that would be included in the `Farkle` package. An MSBuild hook would add these files to the compiled sources, if the project is an F# project (thanks to the `CompileBefore` property we will ensure that these files are visible to the project's own sources). That F# API will be compiled and validated as part of the F# tests.

#### Advantages

* No dependency to `FSharp.Core` for C# users.
* The F# API can use all of F#'s constructs.
* The F# API is not required to be binary-compatible.

#### Disadvantages

* ~~Because no other packages are known to employ this approach, there is some uncertainty around it with regards to edge cases.~~ [Never mind.](https://github.com/NuGet/Home/issues/4229#:~:text=4.x%20ensures%20everything%20is%20in%20dependency%20order%20and%20has%20the%20right%20behavior%20now.)
    * ~~If for example package A depends on package B, and both have MSBuild hooks, is it guaranteed that B's hooks will be imported before A's?~~
        * ~~This is important to ensure that other packages that might employ this approach will not break when a top-level project uses them.~~

### Option 3: Provide a separate NuGet package

The F# API would be provided in a separate NuGet package called `Farkle.FSharp`, that would be reference the main `Farkle` package.

#### Advantages

* No dependency to `FSharp.Core` for C# users.
* The F# API can use all of F#'s constructs.
* This is a common and well-established approach.

#### Disadvantages

* Requires a separate NuGet package, something that we want to avoid.
    * There is not even a case to be made for a separate package, the F# API's code would be tiny compared to the main package.

### Outcome

Option 3 was never seriously considered. The initial plan, also stated in the [C# rewrite document](csharp-rewrite.md), was to go with Option 1 but after reconsideration we will follow Option 2, primarily because it has no known disadvantages and best suits the F# API's role as a thin layer on top of the C# API.
