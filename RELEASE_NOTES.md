#### 4.0.0
* The grammars can be serialized and embedded inside code. No more EGT files are necessary at runtime!
* The reader for GOLD Parser Enhanced Grammar Tables is now based on a Binary Reader, and is faster and more lightweight. __To be confirmed__
* The parser directly generates Abstract Syntax trees, eliminating the need to convert them from reductions, a very hard-to-use type.
* The code was yet again simplified and reorganized into different namespaces.
* The library became much faster after performance profiling.

#### 3.0.0 - 15-07-2018
* Introduced the RuntimeFarkle API. This allows the user to both parse a string and convert its Abstract Syntax Tree into any type easily.
* Farkle does no more depend on Chessie, using the core library's Result type.
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
