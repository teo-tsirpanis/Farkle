#### 3.1.0-alpha004
* Optimized the way Farkle handles the input stream characters by reducing copies.
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
