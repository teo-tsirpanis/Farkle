#### 6.3.0
* __Minor breaking change:__ Text with legacy CR line endings is no longer supported. Since this version, they will no longer be recognized as line endings by position tracking. When Farkle's next major version gets released, they will cause lexical errors.
* The precompiler now works on Visual Studio for Windows. In this case only, it requires to install the .NET tool `Farkle.Tools`.
* Operator scopes will now correctly recognize multiple representations of the same designtime Farkle, such as an original terminal and a renamed one, or a string, and a designtime Farkle created with the `literal` operator that got passed the same string.

#### 6.2.0 - 17-05-2021
* __Breaking change:__ The `Farkle.Builder.LALRBuildTypes` module, as well as most functions of the `Farkle.Builder.LALRBuild` module became internal.
* __Minor breaking change:__ Whitespace inside the "between", "at least" and "exactly `n` times" regex string quantifiers is no longer allowed. Using string regexes like `\d{ 4}` will cause an error.
* __Minor breaking change:__ The `Farkle.Builder.IRawDelegateProvider` interface and a function in the `Farkle.Builder.DFABuild` module became private. They were public by accident and served no purpose for Farkle's users.
* ~~__Minor breaking change:__ Users that write their own tokenizers must ensure that CRLF line endings are `Advance`d at once, otherwise the character stream's position will be incorrect. The vast majority of use cases (those that doesn't involve custom tokenizers) will not be affected by this change.~~ Reverted in Farkle 6.3.0.
* __Minor breaking change:__ Terminals, literals, and groups that are case-insensitively named `NewLine` will have an underscore prepended to their name when built, making them `_NewLine` for example. This change is a temporary workaround for a design deficiency of Farkle, where these terminals could end line groups and comments. It will be thoroughly fixed in the next major version. Because these names are only used for diagnostics and documentation generation, parser behavior will not be affected by this change. Nor will grammars read by EGT files be changed.
* __Minor breaking change:__ The `\s` and `\S` string regex symbol will now match exactly either horizontal tabs, line feeds, carriage returns or spaces. Other characters like vertical tabs and non-breaking spaces are no longer matched. This change matches Farkle's definition of whitespace. To revert to the previous behavior use `\p{Whitespace}` or `\P{Whitespace}`.
* Farkle's string regexes got many improvements and bug fixes, bringing their syntax closer -but not a 100% match- to popular regex languages. Take a look at [the string regex reference](string-regexes.html) for more details.
* The precompiler now works when used on a project targeting a framework earlier than .NET Core 3.1.
* The `Position.Advance` method got a new overload that accepts a `ReadOnlySpan` of characters. It is recommended over the existing one that accepts a single character because it reliably handles line endings.
* Building a grammar can now be cancelled by new functions introduced in `Farkle.Builder`'s `DFABuild`, `LALRBuild` and `DesigntimeFarkleBuild` modules. Additionally the `Build` and `BuildUntyped` extension methods of designtime Farkles now accept an optional cancellation token.
* Fixed a bug where the wrong error position was sometimes reported on text with LF line endings.
* Fixed a bug where the wrong error position was reported on syntax errors.

#### 6.1.0 - 05-03-2021
* Some typos were fixed in Farkle's HTML templates.
* The `ParserApplicationExceptionWithPosition` type was deprecated in favor of a new constructor in `ParserApplicationException`.
* The EGT file reader will throw an exception if it tries to load a grammar file that shifts on EOF in one of its LALR states (which would cause infinite loops).
* The designtime Farkles that are returned by the `many` and `many1` operators now have the correct name.
* Throwing a `ParserException` during post-processing will not be thrown in user code inside a `PostProcessorException`, but will be caught by the runtime Farkle API, allowing to customize the parser error message in greater detail.

#### 6.0.0 - 23-02-2021
* __Breaking change:__ The functions `RuntimeFarkle.ofEGTFile` and `RuntimeFarkle.ofBase64String`as well as their corresponding `RuntimeFarkle` static methods  were removed. Users are advised to migrate away from GOLD Parser.
* __Breaking change:__ The `RuntimeFarkle.GetBuildError` method was replaced by `GetBuildErrors`, which returns a list of build errors.
* __Breaking change:__ The `Farkle.Grammar.Grammar.Properties` member now holds a strongly-typed record of informative grammar properties. Unrecognized GOLD Parser properties such as "Author", "Description" and "Version" are discarded. Existing grammar files remain compatible.
* __Breaking change:__ The members of Farkle's discriminated unions can no longer be accessed in F# using a method. For example, `let foo (x: Terminal) = printfn "%s" x.Name` becomes `let foo (Terminal(_, name)) = printfn "%s" name`. For C# users, duplicate properties like `Terminal.name` with a lowercase "n" were removed; they are replaced by their corresponding title-cased properties.
* __Breaking change:__ The members of many of Farkle's discriminated unions got meaningful names. C# code using the older names might break.
* __Breaking change:__ Removed support for generating legacy grammar skeleton files from MSBuild using the `Farkle` item.
* __Breaking change:__ The `Farkle.Common.SetOnce` type became internal.
* Farkle's types and methods support [C# 8.0's Nullable Reference Types](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#nullable-reference-types).
* Farkle supports virtual terminals -terminals that are not backed by the default tokenizer's DFA but created by custom tokenizers-, allowing for scenarios like indent-based grammars. [An F# sample of an indent-based grammar](https://github.com/teo-tsirpanis/Farkle/blob/master/sample/Farkle.Samples.FSharp/IndentBased.fs) was published.
* Dynamic code generation will be applied to post-processors that are frequently used, in a fashion similar to .NET's tiered compilation, regardless of whether their designtime Farkle is precompilable.
* Build error reporting is improved. More build errors will be reported at the same time, without having to fix one to show the next.
* Stack overflows when building extremely complex designtime Farkles were either mitigated or will throw recoverable exceptions.

#### 6.0.0-alpha.3 - 25-11-2020
* __Breaking change:__ .NET Framework 4.5 is no longer supported. The library targets .NET Standard 2.0, .NET Standard 2.1 and .NET 5.
* __Breaking change:__ Logging parser events is no longer supported. The logging function was removed from the signatures of parser APIs.
* __Breaking change:__ The function `LALRParser.parseLALR` was renamed to `LALRParser.parse`.
* __Breaking change:__ The `Farkle.Parser.OptimizedOperations` type was made internal.
* __Breaking change:__ Transformers no longer take the position of a terminal, but an `ITransformerContext` type containing more information.
* __Breaking change:__ Farkle's exception types were refactored. They inherit from `FarkleException`.
* __Breaking change:__ Post-processor exceptions are wrapped inside a `PostProcessorException` and thrown to user code.
* __Breaking change:__ Only objects of type `PrecompilableDesigntimeFarkle` can be marked for precompilation.
* __Minor breaking change:__ The `Transformers` and `Fusers` properties were removed from the `Farkle.Builder.GrammarDefinition` type.
* Farkle will now optimize precompilable designtime Farkles using dynamic code generation. This feature is only supported on .NET Standard 2.1 if the runtime supports dynamic code compilation.
* An API for the `CharStream` type was published.
* [An API for getting precompiled grammars](https://teo-tsirpanis.github.io/Farkle/reference/farkle-builder-precompiledgrammar.html) was published.
* The tokenizer can be extended by user code.
* The runtime Farkle's extension methods became regular methods.
* ~~Exceptions that might occur when a precompiled grammar fails to be loaded can be suppressed with an `AppContext` switch.~~ __Removed in Farkle 6.0.0__

#### 6.0.0-alpha.2 - 23-08-2020
* Farkle supports creating regexes from strings. See more in https://teo-tsirpanis.github.io/Farkle/string-regexes.html.
* The parser, the builder and the EGT(neo) reader became faster once again.
* The precompiler does not crash when used from .NET Standard libraries.
* __Breaking change:__ The `CharStream` API became internal. A new one will be published in the next release.

#### 6.0.0-alpha.1 - 13-04-2020
* __Breaking change:__ Removed the legacy API for creating runtime Farkles from EGT files (the API with the transformers and fusers). EGT files are still supported (for now), but users are strongly urged to rewrite their grammars using `Farkle.Builder`, or implement the `PostProcessor` interface themselves (not recommended).
* __Breaking change:__ The `PostProcessor` type was moved to the root `Farkle` namespace. Some reusable post-processors were moved to the new `Farkle.PostProcessors` module.
* Farkle can now build grammars at compile-time. See more in https://teo-tsirpanis.github.io/Farkle/the-precompiler.html.
* Added a function to rename designtime Farkles; it might be useful for better diagnostic messages.

#### 5.4.1 - 23-03-2020
* Refactor some designtime Farkle functions (like `many1`) to use less nonterminals.
* Add functions and regexes to create terminals for unsigned real numbers.

#### 5.4.0 - 20-03-2020
* You can now add lexical groups in a grammar. They resemble [GOLD Parser's feature](http://www.goldparser.org/doc/grammars/define-groups.htm), but always advance by character and do not not support nesting.
* Add a couple of methods in runtime Farkles to easily check whether building it had succeeded.
* Lexical errors are reported at the point they occur; not at the point of the first character read by the tokenizer.
* Made the untyped builder API easier to access. See the deprecation notice for the new functions to use.
* __Minor breaking change:__ The `CharStream.readChar` value gets the character index by value, not by reference. Callers have to increment it accordingly to get further characters.
* __Minor breaking change:__ The type `Farkle.Grammar.OptimizedOperations` was moved to the `Farkle.Parser` namespace; it was there for historical reasons.
* As you might have seen, breaking changes on public members that do not affect the average Farkle user will not warrant a major version increase.

#### 5.3.0 - 23-02-2020
* Farkle's speed __more than doubled__ by disabling tailcall optimizations.
* __Minor breaking change:__ The API of the `CharStream` type slightly changed. Most notably, the type `CharStreamIndex` was removed in favor of `uint64`, and the order of the last two arguments in the function `CharStream.read` has changed.

#### 5.2.0 - 09-02-2020
* The types `DesigntimeFarkle<TResult>` and `PostProcessor<TResult>` are covariant. The change was made possible by [Covarsky](https://github.com/teo-tsirpanis/Covarsky), a tool written for this purpose.
* Added a function called `Regex.allButChars` (`Regex.NotOneOf` for C#) that creates regexes accepting all characters except certain ones.
* User code exceptions during post-processing are not captured anymore.
* Farkle.Tools.MSBuild works with all .NET Core SDK versions after 2.0.

#### 5.1.0 - 31-01-2020
* It happened. Farkle can create grammars without the need of GOLD Parser. __Farkle is now a parsing library on its own.__
* Move the `CharStream` type in the `Farkle.IO` namespace.
* Add methods to parse text from .NET `TextReader`s. They should be preferred over parsing .NET `Stream`s because the latter are supposed to contain binary data, not text.
* The `Farkle.CSharp` namespace is no longer required. C# users just have to use `Farkle` to get their extension methods, unless they are writing their own post-processors for GOLD Parser grammars, where they have to use `Farkle.PostProcessor.CSharp`.
* __Breaking change:__ Farkle.Tools.MSBuild was upgraded to .NET Core 3.1. Nothing significant changed though, which means those who still use .NET Core 2.1 can stay in a previous version.
* __Breaking change:__ Reading grammars from EGT files now raises an exception.
* __Breaking change:__ Some utility functions that had nothing to do with parsing were either removed or made internal.
* __Breaking change:__ Internal errors of the parser (in the unfortunate case they happen) throw an exception. In the next release, exceptions in a transformer or fuser will not be caught either.

#### 5.0.1 - 21-08-2019
* Fix a bug where comments in input text would sometimes crash the parser.
* Allow line comments in the last line of the input text.

#### 5.0.0 - 13-08-2019
* Fix a bug where the tokenizer would erroneously report an EOF instead of a lexical error. - [#8](https://github.com/teo-tsirpanis/Farkle/issues/8)
* Add `CharStream.TryLoadFirstCharacter`. With this method, you can check whether the input of a character stream has ended, and safely access `CharStream.FirstCharacter`.
* Bring back `Grammar.StartSymbol`, but implement it correctly this time.
* Write more tests and documentation.

#### 5.0.0-rc.7 - 10-08-2019
* Speed-up the tokenizer by using an array that handles ASCII characters.
* Remove `Grammar.StartSymbol`, as it was unreliable, and useless to Farkle.
* Remove the `OutputDirectorySuffix` metadata introduced in 5.0.0-rc.5. Generated source files by Farkle.Tools.MSBuild are _always_ added in the same directory as the project.

#### 5.0.0-rc.6 - 22-07-2019
* Fix a breaking error in Farkle.Tools.MSBuild.
* Reduce allocations in the parser.

#### 5.0.0-rc.5 - 22-07-2019
* Fixed a bug which sometimes made Farkle.Tools.MSBuild unusable for C# users.
* Added a new MSBuild item metadata called `OutputDirectorySuffix` which is useful for generating templates in a different directory than the EGT file.
* Add a new benchmark which compares a Farkle-made JSON parser to [Chiron](https://xyncro.tech/chiron/), which is powered by [FParsec](https://www.quanttec.com/fparsec/). You can see the results of the benchmarks [over here](https://github.com/teo-tsirpanis/Farkle/tree/master/performance).

#### 5.0.0-rc.4 - 18-07-2019
* Fixed a bug which made Farkle.Tools.MSBuild unusable.

#### 5.0.0-rc.3 - 17-07-2019
* The CLI tool got a new property `--namespace` (shortened to `-ns`) which is simpler to use and replaces `--property`.
* Fixed errors in the generated F# code.

#### 5.0.0-rc.2 - 17-07-2019
* The CLI tool generates post-processor skeletons by default.
* The CLI tool can automatically find the EGT file to process (if there is only one in the current directory), and the language (if there are C# or F# projects in the current directory).
* Fixed a bug where generated source files would sometimes have duplicate production names in their enumerated type. - [#7](https://github.com/teo-tsirpanis/Farkle/issues/7)

#### 5.0.0-rc.1 - 15-07-2019
* Farkle now has better C# support. Just write `using Farkle.CSharp;`, and you are good to go!
* Farkle now has a CLI tool helper. It can generate a grammar definition file that contains the terminal and production types for your grammar, as well as the EGT file in base-64 encoding. It can also create a skeleton source file to help you write a post-processor. What is more, it supports _both_ C# and F#!
* Farkle now has MSBuild integration. You can auto-generate a source file describing your grammar, and not have to carry EGT files around.
* As always, performance was improved, especially in the EGT file reader.
* __Breaking change:__ In your post-processor, if you have functions like `take2Of production (index1, index2) count func`, remove the `count` parameter.
* __Breaking change:__ The `Token` type was moved to `Farkle.Parser` and is not needed by the `AST` type.

#### 4.0.2 - 21-08-2019
* Fix a bug where comments in input text would sometimes crash the parser.

#### 4.0.1 - 11-08-2019
* Backport the fix of GitHub issue [#8](https://github.com/teo-tsirpanis/Farkle/issues/8).

#### 4.0.0 - 17-01-2019
* Optimized the way Farkle handles the input stream characters by reducing copies & improving performance.
* Removed all 3rd-party non-Microsoft dependencies.

#### 3.1.0-alpha003 - 10-10-2018
* The `GOLDParser` API was replaced in favor of the `RuntimeFarkle` type. This means that the parsing and post-processing operations are unified.
* The code became cleaner and faster yet again, with a notable optimization in the tokenizer.
* This release coincided with the author's birthday. 🎂

#### 3.1.0-alpha002 - 14-09-2018
* The EGT file reader was replaced with a newer one which is significantly more performant.
* The `GOLDParser` class was removed in favor of the new module with the same name.

#### 3.1.0-alpha001 - 06-09-2018
* The versioning scheme changed. Even (and zero) minor and patch versions signify stable releases, while odd ones signify unstable releases. For example, this is an unstable release.
* The functions for the `RuntimeFarkle` type are in their own module.
* The reader for GOLD Parser EGT files is now based on a BinaryReader, and is faster and more lightweight.
* The parser directly generates Abstract Syntax trees, eliminating the need to convert them from reductions, a very hard-to-use type.
* The parser does not keep the parsing log messages. Instead, a callback that is provided by the user is called from which the message can be arbitrarily processed.
* The code was yet again simplified and reorganized into different namespaces.
* The library became much faster after performance profiling.

#### 3.0.0 - 15-07-2018
* Introduced the `RuntimeFarkle` API. This allows the user to both parse a string and convert its Abstract Syntax Tree into any type easily.
* The parser can lazily read an input file.
* The project does no more depend on Chessie, using the core library's Result type.
* The internal architecture was greatly refactored.

#### 3.0.0-alpha001 - 06.09.2017
* A new type named ParseResult will make parsing easier for both F# and C# users.
* Changed the way the index of grammar objects is stored.

#### 2.0.0 - 03.09.2017
* Overhauled the low-level parser API. It is a simple 5-line type called `Parser`. Its new design prevents misusing the API (like continuing parsing on a completed parser state), and decouples it from any implementation.
* Also, the GOLDParser class is changed too. It is no more a static class, and its design encourages creating a grammar only once.
* Polished the API; changed the names of some types (like `Production.Nonterminal` to `Head`), added prettier C# names and made some types internal.
* And last yet not least... _Everything_ is documented.

#### 2.0.0-beta001 - 18.08.2017
* The project is now one NuGet package named Farkle.
* The library became faster after performance profiling.

#### 1.0.0 - 08.08.2017
* Initial release.

#### 0.0.1-alpha
* Started the project.
