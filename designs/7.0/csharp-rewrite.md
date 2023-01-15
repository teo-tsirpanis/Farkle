# Porting Farkle to C#

## The F# Tax

Farkle started as a GOLD Parser engine for F#, gaining more features over time and becoming autonomous a couple of years later. Besides being the language I was at that time enamored with, F# had the advantage of brevity, that helped Farkle rise. As of writing this document, the main Farkle project consists of only 11.277 lines of F# code; with C# it would have been much higher.

However it hasn't been all rosy. The use of F# brought several drawbacks, that can be summarized as "the language is not low-level enough for a project like Farkle". I call these drawbacks the "F# Tax" and some of them are the following:

* F# misses a couple of features that C# has and we want to use. These features mainly revolve around interoperability with C# and F#. To use them we had to use many workarounds some consider to be unnatural.
    * Several of Farkle's generic interfaces and delegates have to be covariant, something that F# does not support. To do that, [a weaver](https://github.com/teo-tsirpanis/Covarsky) was created that does the trick after compilation.
    * F# does not support cyclic references between files. Sure, it's a bad practice, sure, it enforces good design, sure, you can have cyclic references within a file, but if the string regexes are defined in a grammar that has to be built, and the builder has to support string regexes, what do you do? [Reflection!](https://github.com/teo-tsirpanis/Farkle/blob/c0593a8e17edee6404baaf2a679defce5da47106/src/Farkle/Builder/DFABuild.fs#L22-L31)
    * A small thing that nevertheless showcases F#'s weirdness is that [F# functions cannot be converted to delegates without an extra layer of indirection](https://github.com/fsharp/fslang-suggestions/issues/1083). The solution is once again reflection.
    * Not a missing feature but another weird thing is that the F# compiler inlines functions in the same assembly and even across assemblies. When I added trimming annotations to the function that solved the above problem, it got inlined and the warning suppressions had no effect. [I had to mark it with `NoInlining`.](https://github.com/teo-tsirpanis/Farkle/blob/47ff2f7caae6f8f646e2664a007ad2d28f2c1aa7/src/Farkle/Common.fs#L132-L133)
    * [The language has some weird rules around static field initialization.](https://github.com/dotnet/fsharp/issues/9719)
    * Lack of `protected`.
    * Lack of pinning arbitrary `byref`s.
    * Lack of lightweight optional values. `Option` allocates and `ValueOption` has overhead. C#'s nullable reference types are by far superior.
    * …
* Any assembly compiled with F# has to use `FSharp.Core`, a package that weighs two and a half megabytes. Being a low-level library, Farkle depending on `FSharp.Core` is problematic. [BitCollections](https://github.com/teo-tsirpanis/BitCollections), another dependency of Farkle is used only by the grammar state machine builders, and can be entirely removed with trimming when using the precompiler. By contrast, the use of `FSharp.Core` is necessarily so pervasive that it cannot be entirely trimmed away.
* F#'s tooling ecosystem is subpar compared to C#. The compiler is slower, the compiled assemblies are bigger, there is no support for first-party analyzers or source generators, support for XML documentation tags is lacking and there are issues around deterministic packages and Source Link.

## Towards clarification[^clarify]

To solve all the above problems, Farkle 7 will be rewritten to C#. Besides the advantages listed above, another advantage is that this language change will help with Farkle's big refactoring. We will need to implement only the new grammar, parser and precompiler APIs without worrying about existing code being broken in between.

### Commitment to supporting F#

These changes will not undermine the priority to provide a first-class F# API for Farkle. The library will just move from an F# library with an additional C# API, to a C# library with an additional F# API. The most likely way to deliver it is by depending on `FSharp.Core` and adding attributes that enable things like currying, and overloads that take `FSharpFunc` in addition to delegates. This will not allow completely removing the dependency to `FSharp.Core`, but since the library will be used only as part of the F# API surface, a much bigger part of it will be able to be trimmed away in C# programs. I have submitted pull requests in `dotnet/fsharp` to improve `FSharp.Core`'s trimmability.

### Disadvantages

Let's talk about the disadvantages:

* __Increased codebase size.__ Since C# is more verbose than F#, Farkle's codebase is expected to be increased; by how much it is unknown. This is acceptable; for a project like Farkle keeping the codebase small is of less priority.
* __Inability to use SRTPs.__ [Statically Resolved Type Parameters](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters) are F#'s generics on steroids that are resolved at compile time and support constraints on members with a specific signature. There is no easy way to write methods with SRTPs from C#. Farkle uses SRTPs in two places; the `prec` operator, and the `Terminals.generic(Real|Signed|Unsigned)` family of functions. Converting `prec` is easy; we can make it a regular generic function and have production builders implement an interface, but the generic numerical terminal factory methods will have to be implemented with static abstract functions, making them available only on .NET 7+, but compatible with C# as well.

### The process

During the rewrite to C#, we don't want to throw away the existing F# codebase, and we want to ensure it does not break. The rewriting process will be as follows:

* A new C# project with the name `FarkleNeo` will be created, that will contain the code of the new Farkle. It will have its own C# test suite.
* The existing F# code and tests will run in a separate CI workflow that will be skipped if only the new C# code is changed.
* Once `FarkleNeo` is feature-complete, it will be renamed to `Farkle`, the F# Farkle project will be removed, and the rest of the F# projects will migrate to the new Farkle.
    * My original plan was to not release a preview until all features are complete, but maybe we can release a preview without the precompiler.
* After we implement the new precompiler (not sure of the language), we will migrate the MSBuild integration tests.
* At a later time we can rewrite the CLI tool, the precompiler (if we already didn't), and the templating engine to F#. `Farkle.Tests` will not be rewritten.

[^clarify]: Just like how converting code to Rust is called "oxidizing", I propose converting code to C# be called "clarifying" (because you can "see the code sharp").
