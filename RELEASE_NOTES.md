#### 5.0.0-rc.8
* Fix a bug where the tokenizer would errorneously report an EOF instead of a lexical error. - [#8](https://github.com/teo-tsirpanis/Farkle/issues/8)
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

#### 4.0.1 - 11-08-2019
* Backport the fix of GitHub issue [#8](https://github.com/teo-tsirpanis/Farkle/issues/8).

#### 4.0.0 - 17-01-2019
* Optimized the way Farkle handles the input stream characters by reducing copies & improving performance.
* Removed all 3rd-party non-Microsoft dependencies.

#### 3.1.0-alpha003 - 10-10-2018
* The `GOLDParser` API was replaced in favor of the `RuntimeFarkle` type. This means that the parsing and post-procssing operations are unified.
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
