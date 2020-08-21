# Choosing a parser for your .NET project

Besides Farkle, there are many other general-purpose parsing projects for the .NET ecosystem. Farkle's main competitors are FsLexYacc and FParsec to a lesser degree. In this guide we will examine the strengths and weaknesses of each parser library and help you decide which one to use for your next project. So, are you ready? Let's do this!

## Farkle vs FParsec vs FsLexYacc

The comparison between Farkle, [FParsec] and [FsLexYacc] is outlined in the following table. A more detailed explanation is following.

> __Note:__ All projects we will discuss support .NET Standard.

| |Farkle|FParsec|FsLexYacc|
|-|-|-|-|
|Type|Library|Library|Parser generator|
|Grammar definition language|Code|Code|Separate files|
|Parsing algorithm|LALR|Recursive descent|LALR|
|Lexer|Less decoupled|No|More decoupled|
|Lexing algorithm|DFAs|N/A|DFAs|
|Left recursion support|Yes|No|Yes|
|Operator precedence support|Planned|Yes|Yes|
|Whitespace/Comment handling|Easy|Harder|Hard|
|Parsing speed|Good|Good|Subpar|
|MSBuild integration|Optional|N/A|Required|
|C# support|Yes|No|No|
|Maturity|Ever-evolving|Mature|Mature|

### Grammar definition language

FsLexYacc follows more closely the paradigm of the traditional `lex`/`yacc` tools and its users write their grammars in separate files (having the extension `.fsl` and `.fsy`). These files are not F# code which means that they do not support features like IntelliSense or instant error reporting.

In Farkle and FParsec, the languages are specified in code, resulting in higher productivity and a smoother learning curve. Furthermore, the grammars for these languages are first-class language objects that can be easily composed and extended. This is very hard to do in parser generators like FsLexYacc.

### Parsing

FParsec uses parser combinators which are little functions that parse fragments of text and can be composed into bigger ones. FParsec's parsers are therefore the most flexible of the three libraries, allowing context-sensitive grammars with infinite lookahead and their behavior can be fully customized by arbitrary code. This immense flexibility parser combinators bring to the table makes the library user responsible to keep their parser's performance adequate and predictable. Furthermore, parser combinator libraries use a form of recursive descent parsing, prohibiting the use of left recursion.

The other two libraries use a universal LALR parser whose behavior is customized with parsing tables generated from a grammar. While it restricts the allowed grammars to those who adhere to a set of parsing formalisms, this erga omnes approach makes the parser's performance more predictable, guaranteeing it will finish in linear time, but it also means that if the parser is slow, there are little things the user can do; only the library's author can make it faster. Custom code can be injected in far fewer places than anywhere, namely when the parser reduces a production and between the lexer and the parser. Context-sensitive grammars can still be created with a bit of creativity.

As stated before, FsYacc uses an external grammar definition language. Farkle sits between FsYacc and FParsec, supporting composing grammars from smaller grammars (called _designtime Farkles_). Designtime Farkles however are not parser combinators; they are backed by a formal grammar element, either a terminal or a nonterminal.

Parsing text with all three libraries ends up with the parser returning a custom object, usually representing an Abstract Syntax Tree (AST). Farkle supports easily changing the returned object of a grammar (for example to just perform syntax checking without creating an AST).

### Lexing

FParsec does not use a discrete lexing stage, performing both parsing and lexing at the same time.

In Farkle and FsLexYacc, lexing (called _tokenizing_ in Farkle) is performed separately from the actual parsing. Both projects use Deterministic Finite Automata (DFAs) to perform the lexing based on regular expressions, but Farkle uses a more efficient algorithm that generates smaller automata than FsLexYacc.

In FsLexYacc, the lexer is a completely separate product simply called FsLex from the parser which is called FsYacc. One could use a custom lexer with FsYacc or the opposite.

In Farkle the tokenizer and the parser are separate but using them separately to extend the tokenizer's power is hard. An API to make it easier will be available in a future release, but Farkle already has first-class support for advanced lexing features like lexical groups, noise symbols and comments.

### Operator precedence support

FParsec and FsYacc support operator precedence and associativity to more intuitively write grammars (and automatically resolve LALR conflicts in the latter case). The former does it [via a special type][FParsec-operators], and the latter has direct support in the grammar definition files.

Farkle does not yet support operator precedence but will add it in a future release. Until then, productions will have to be written in a way that does not produce LALR conflicts.

### Whitespace/comment handling

FParsec's approach to handling whitespace and comments is the most tedious of the three. Users have to manually state every place whitespace might appear. Comments are not directly supported either.

Comments in FsLex are still nontrivial but more manageable since their presence does not affect FsYacc at all, thanks to the lexer/parser separation.

Farkle ignores whitespace by default with an option to disable. __In fact, automatic whitespace handling was one of the features that FParsec didn't have and prompted Farkle's creation.__ It also supports adding both line and block comments in a grammar with just one line of code. Furthermore, more complicated symbols that match a regex (called _noise symbols_) can be automatically ignored (such as pre-processor pragmas).

### Parsing speed

This is quite the controversial topic. Farkle was made with performance in mind, lots of time has been invested to increase it, and it's getting faster with each release. [Its performance is compared][Farkle-benchmarks] against the other two libraries by parsing a 74KB JSON file with Chiron (which uses FParsec), Farkle and FsLexYacc.

On such a big file, Farkle was shown to be faster than the other two libraries, but on very small JSON files, FParsec was winning. FsLexYacc was in both cases the slowest of the three.

> __Note:__ FParsec comes in another flavor called "Big Data Edition" which uses unsafe code for increased performance. The performance of that alternative edition was not taken into account because it would be unfair towards the other two libraries that don't use unsafe code.

> __Disclaimer:__ The performance of a parsing library is a subjective value that depends on the hardware it is running, the way the application uses the library and lots of other things. Parsing JSON files is just a narrow measurement. If you are writing a parser for a performance-sensitive app, you should profile it yourself and only trust your numbers.

### MSBuild integration

FParsec does not have any reason to integrate with MSBuild.

FsLexYacc requires a tool to generate the source files for the lexer and the parser (there is a reason it's called a parser _generator_). Fortunately this tool can be integrated with MSBuild and transparently generate the source files when the project is built.

Farkle integrates with MSBuild [to generate the parsing tables for a grammar ahead of time](the-precompiler.html) for increased start-up performance and error checking. It does not generate any source file but serializes the grammar into a binary file which is embedded in the compiled assembly. This feature is totally optional. Moreover, Farkle's MSBuild integration is more robust than FsLexYacc's, using custom MSBuild tasks, instead of FsLexYacc calling external command-line tools. In a future release, more things will be possible with Farkle and MSBuild.

### C\# support

While all three libraries support parsing text from C# with a grammar written in F#, Farkle is the only of them [to fluently support C# for creating grammars](csharp.html).

It is impossible to generate C# code from FsLexYacc without substantially modifying the tool and it almost certainly is not a feature worth implementing.

Since FParsec is just a library and does not require tooling support, C# users can theoretically write an FParsec parser but the sheer amount of F# custom operators and idiomatisms it uses would definitely result in very unreadable code.

### Maturity

Another tough topic. FParsec and FsLexYacc are mature projects, used in various applications for a long time, and their feature set seems stabilized. Owing to their longevity, they have a community around them. Even Microsoft is using them in some of its products: FParsec in [the parser][QSharp-parser] for the Q# programming language and FsLexYacc in [the parser][FSharp-parser] [and lexer][FSharp-lexer] for F# itself.

Farkle on the other hand is relatively new. It started being developed in 2017 and was not a standalone parsing library until version 5.1.0 was released in January 2020. It is still actively developed, with lots of big features slated to arrive. It also means that Farkle's API is still unstable, with minor breaking changes even in minor releases. Its development is a one-man show but other developers are more than welcome to contribute.

## Some C\# parsing projects

To further convince the indecisive C# users to use Farkle, we will also take a brief look at some parsing libraries made for C#.

> __Disclaimer__: Not all the libraries below were actually tried. And this is not an objective evaluation, but more of a subjective review. If any comment made is wrong, feel free to open a GitHub issue or pull request to fix it.

### [Sprache]

Sprache is a parser combinator library, just like FParsec. It creatively uses the LINQ query expression syntax to easily define parsers.

The problem with Sprache is that it is slow. Sprache's performance was going to be benchmarked against the three other libraries but the code was not committed because it was significantly slower than all of them.

### [Irony]

Irony is interesting. Like Farkle, it uses the LALR algorithm while being a library, not a parser generator, requiring no external tools. Its API is object-oriented; each grammar is a subclass of a `Grammar` class and the initialization is performed at the constructor and takes advantage of overloading the `|` and `+` operators to succinctly define productions, more simply than the admittingly verbose C# API of Farkle. It supports features like comments and operator precedence. The lexer's output can be customized by what Irony calls `Scanner Filters`.

Irony is used by Microsoft to parse Android API definition files and generate the bindings for `Xamarin.Android`.

Unlike Farkle however, Irony's grammars are not composable, and unlike all the three libraries, Irony does not support returning strongly-typed objects from grammars. Instead, each nonterminal (not production) can be assigned a delegate via the `AstConfig.NodeCreator` property which forces the user has to manually cast the production's members from object and combine them. Irony's performance was not evaluated for this guide but might be in the future.

A more serious problem with Irony is that its development is fragmented between two projects called `Irony` and `Irony.NetCore` (eventually the first package got .NET Core support as well), making it a little hard to choose which one to use.

### [ANTLR]

The Abrams tank of parser generators, ANTLR is a tool written in Java but supports generating parsers for other languages including C# but not F#. A very mature and well-known project, it uses a variation of the LL algorithm called Adaptive LL (ALL) that supports left recursion.

ANTLR's problems are the same with any parser generator, mainly looser integration between the grammar and the rest of the program. The language independence ANTLR and other tools offer is good but not really amazing; only large projects would make use of that feature. ANTLR's use of syntax tree listeners and visitors steeps up the learning curve compared to Farkle, FsLexYacc or a parser combinator library. Furthermore, using ANTLR to a .NET project would add a dependency to Java in its build process.

It is this guide's recommendation to use ANTLR only if other parsers are inadequate for the project and after carefully considering the benefits and drawbacks ANTLR would bring.

### GOLD Parser

GOLD Parser's approach is rather unique. Instead of generating source code for each language it supports (though it optionally supports that too), it creates a binary file with the grammar's parsing tables which can be read by libraries called _engines_ that can be written in any language. Farkle started as an engine for GOLD Parser, even though its support for GOLD Parser is being gradually phased out.

The biggest problem with GOLD Parser is that it is unmaintained. The latest version of the builder (the tool that creates the grammar tables) was released in 2012. Moreover, the tools are running on .NET Framework but can run in non-Windows operating systems with Mono. And these tools are closed-source, under a license that looks like MIT. For these reasons, GOLD Parser is not recommended for new projects. Older ones are encouraged by this guide to migrate to other parsers for increased performance and maintainability.

## When to use a specialized parsing library

A parsing library or parser generator like the ones we described above is not a silver bullet. Sometimes using a parser that specializes in parsing one file format is preferable.

* __There is already one:__ If you are going to parse a well-known and popular file format and a library that parses it already exists, chances are that using this library is better than writing your own parser and would provide better performance and facilities suited for that format. You wouldn't write a parser for JSON, XML, HTML, C# or F#, would you? Farkle's codebase has a JSON parser for benchmarking (two identical ones actually, written in C# and F#) but will not be published to NuGet. `Newtonsoft.Json` and `System.Text.Json` are much faster JSON parsers than Farkle's and have much more features to warrant another JSON parser being released.

* __Custom binary files:__ This is a bit controversial, as some parser combinator libraries support parsing binary files, but the guide's recommendation is to write your own binary file parser using framework classes like `Stream` or `BinaryReader` for increased performance, which is the main reason to use binary files in the first place.

---

So I hope you enjoyed this little comparison. If you did, don't forget to give Farkle a try, and maybe you feel especially left-recursive today, and want to hit the star button as well. I hope that all of you have a wonderful day and to see you soon. Goodbye!

[FParsec]: https://www.quanttec.com/fparsec/
[FsLexYacc]: https://fsprojects.github.io/FsLexYacc/
[FParsec-operators]: https://www.quanttec.com/fparsec/reference/operatorprecedenceparser.html
[Farkle-benchmarks]: https://github.com/teo-tsirpanis/Farkle/tree/master/performance
[QSharp-parser]: https://github.com/microsoft/qsharp-compiler/tree/master/src/QsCompiler/TextProcessor
[FSharp-parser]: https://github.com/dotnet/fsharp/blob/main/src/fsharp/pars.fsy
[FSharp-lexer]: https://github.com/dotnet/fsharp/blob/main/src/fsharp/lex.fsl
[Sprache]: https://github.com/sprache/Sprache
[Irony]: https://github.com/IronyProject/Irony
[ANTLR]: https://github.com/antlr/antlr4/tree/master/runtime/CSharp
