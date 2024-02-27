# Farkle 7's builder API

Developed in the summer of 2019 and introduced in version 5.1.0, Farkle's builder is the newest of the three major library components (the other being the grammars and the parser). Its design is mostly good, and its general principles will be carried over to Farkle 7. However, there are some things that can be improved. Instead of exhaustively outlining the entire builder API, this document will only cover the changes planned or considered for Farkle 7.

## Global and local options

When building a grammar you can configure it beyond its syntactic and semantic rules. Most of these configuration options are global, which means that they apply to the entire grammar, like comments, case sensitivity and automatically ignoring whitespace. Other options are local, which apply to individual symbols, like renaming them and attaching an operator scope.

Farkle 6 exposed these configuration APIs as extension methods to the `DesigntimeFarkle` type, that create new objects with just one option changed. The problem with it is that there is an implicit contract of the global configuration methods that they should be called only from the topmost `DesigntimeFarkle` that corresponds to the start symbol of the grammar. Setting the options elsewhere causes them to be ignored.

Farkle 7 will refactor the hierarchy of builder objects to address this problem, as well as give them more descriptive names. The new hierarchy will look like this:

```csharp
namespace Farkle.Builder;

// The base class for all builder objects.
// Supports only building and changing global options.
public interface IGrammarBuilder { }

// Like IGrammarBuilder, but also supports returning a result.
public interface IGrammarBuilder<out T> : IGrammarBuilder { }

// Supports using it as a member in a production and changing local options.
public interface IGrammarSymbol : IGrammarBuilder
{
    string Name { get; }
}

// Like IGrammarSymbol, but also supports returning a result.
public interface IGrammarSymbol<out T> : IGrammarSymbol, IGrammarBuilder<T> { }
```

Almost all builder APIs will return `IGrammarSymbol` or its generic counterpart. The only methods that will deal with `IGrammarBuilder` directly are the global option setters and the `Build` method. By accepting and returning `IGrammarBuilder`, the one-way transition from `IGrammarSymbol` is made explicit.

Like before, these interfaces will be considered black boxes and user code will be prohibited from implementing them. This might be made explicit by adding an internal method, if it works in earlier frameworks.

### The scope of operator scopes

In Farkle 6 setting an operator scope is a local option and can be done from anywhere in the grammar. However, if the same symbol exists in multiple scopes, the behavior during parser conflict resolution is undefined. One obvious fix to this is to ask all operator scopes to resolve the conflict and fail if more than one of them can do it but gives contradictory results. Having many scopes poses the question of how to encode them in a grammar, which we will want to do in the future. There is the naïve way of directly encoding the operator scopes, their associativity groups, and their symbols, but maybe we could represent them in a more sophisticated way that is more efficient to read and any contradictions are detected at construction time.

I have pondered this for a significant amount of time in the past, without any concrete idea of this sophisticated implementation, but I am not convinced that such way does not exist. To avoid painting myself in a corner, and also having found no use cases for multiple operator scopes yet, I am inclined on making the operator scope a global option for Farkle 7.

## Remove generic configuration methods

In Farkle 6.3.0 the aforementioned configuration methods became generic to support using them on either typed or untyped builder objects. Besides being weird design-wise, it actually restricts certain scenarios and will not be done again in Farkle 7.

Instead we will take the extra step of creating two pairs of extension methods for typed and untyped builder objects respectively. ~~For F# we will have untyped configuration functions with a `U` suffix, just like the rest of the builder API.~~

__Update__: F# functions will be provided only for setting local options (just renaming symbols at the moment). Setting global options will be done with the same extension methods C# will use. The reason for this is to reduce the amount of code in the F# API, and because in most cases the types of grammar builders and symbols is statically known and does not need type annotations to call extension methods.

## Reconsider case-insensitivity by default

Farkle's builder produces case-insensitive grammars by default, just like GOLD Parser. I am not 100% sure if this is a good idea. Besides the performance cost of "desensitivizing" all the grammar's regexes, not many programming languages have case-insensitive keywords. The default value is likely to change in Farkle 7, and furthermore there will be an option to make only literals case-insensitive.

To allow using case-insensitivity when it is actually needed, specific regexes will be able to be marked as case-sensitive or case-insensitive.

## Improve the string regex language

Farkle 6 introduced an API to create regexes from string patterns. The language for these regexes was based on GOLD Parser's regex language and has several oddities compared to established regex dialects. Two (the only?) examples of this is that whitespace characters are ignored by default unless surrounded by quotes, and the special meaning of the `.` pattern[^any].

For Farkle 7 the string regex language will change to not ignore whitespaces and change the meaning of `.` to actually "any character".

## New terminal kinds

The grammar file format for Farkle 7 specifies two concepts for terminals that did not exist in previous versions:

* Terminals can be marked as _hidden_, which means that they will not be included in the expected symbols list in case of a syntax error.
* The definition of noise symbols became more flexible to allow even regular terminals to be marked as noise, enabling the parser to ignore them if found in unexpected places, instead of failing with a syntax error.
    * This would especially benefit the special `NewLine` terminal. In previous versions, its presence anywhere in the grammar would mean that new lines in places not clearly specified would cause a syntax error, but we can configure it, either according to the "auto whitespace" option or with a dedicated "`NewLine` is noise" option.

The same concepts will be supported for groups. The respective attributes will be applied to the group's container symbol.

## Compatibility levels

This document describes several behavior breaking changes. To allow containing the impact of these changes, we will add a way to specify the version of Farkle the grammar was developed with, and the builder's behavior of that version will be maintained, even if a newer version of the library is used.

Compatibility levels will be introduced with a major or minor version when it brings potentially breaking changes to the default builder behavior. Patch releases will not introduce such changes.

~~Initially two compatibility levels will be introduced: one for Farkle 6 and one for Farkle 7.0. A "latest" compatibility level that will always alias to the newest one will also be introduced.~~ Initially one compatibility level will be introduced for Farkle 7.0, and future ones will be added as needed. Setting a compatibility level will be added as a global option and when creating a string regex (affecting only how the string regex will be parsed). All compatibility levels except for the one for the current version and the "latest" one will be marked as obsolete.

__Update__: After some consideration, it was decided that a compatibility level for Farkle 6 will not be provided. Given the fundamental changes in places like the DFA builder, and the already extensive API changes, providing behavior compatibility with Farkle 6 would not have any significant benefits. The "latest" compatibility level alias will also not be provided; it would be equivalent to not setting a compatibility level. Because the precompiler already provides this shielding from behavior breaking changes, explicit compatibility levels will be recommended to be set only in specific scenarios. This feature was very close to being entirely cancelled, but introducing it later would lose some of its purpose.

## P&A for nonterminals

_Tracked in [#41](https://github.com/teo-tsirpanis/Farkle/issues/41)._

## ~~Uniform F# operators~~

> [!NOTE]
> This is postponed because of [limitations in the F# language](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md).

The use of custom operators F# builder API maybe can be simplified, assuming my guesses about how operator overloading in F# can be implemented:

* The `!&` operator that starts productions with literals can be replaced with `!%`, the same operator that starts productions with a non-significant symbol. The former in particular has a problem of being hard to type in a keyboard.
* The `||=` and `|||==` operators can be extended to set productions in recursively defined nonterminals, avoiding to call the `SetProductions` method.

Here's an example of how the new operators would be used:

__Before__
```fsharp
let nont = nonterminalU "Expression"

nont.SetProductions(
    !& "(" .>> nont .>> ")" .>> nont,
    empty
)
```

__After__
```fsharp
let nont = nonterminalU "Expression"

nont |||= [
    !% "(" .>> nont .>> ")" .>> nont
    empty
]
```

The existing operators will be kept for compatibility reasons and be marked as obsolete.

## Separate symbol and grammar name

In Farkle 6 the name of a grammar is always equal to the name of its start symbol. This results in either the grammar or the start symbol having a name that does not fit well (good luck with naming your grammar `Compilation Unit`). In Farkle 7 the name of the grammar will be a global option and settable with a separate method from the start symbol's name. If the grammar name has not been set, it will still be equal to the name of the start symbol.

## Better-defined symbol renaming

In Farkle 6 if you rename a symbol, you must only use its renamed instance throughout your grammar. Otherwise the name of the symbol Farkle will use will be unspecified. To ease this a bit in Farkle 7, if a symbol is used as both its original instance and one renamed instance, the changed name will always be used. If a symbol is being used as multiple renamed instances, one of them will be used, but it will still be unspecified which one.

## Group nesting

While nesting groups has always been supported in all of GOLD Parser's and Farkle's grammar formats, declaring groups that can be nested within each other has not been supported in Farkle's builder API. We will add APIs for it in Farkle 7.

__Update__: Groups can be defined to nest with themselves. More custom nesting will be implemented at a later time.

## Improve setting productions

Because nonterminals with no productions are not allowed, the `Nonterminal.Create` and `SetProductions` methods enforced at compile-time that at least one production is created, by accepting one production and then a `params` array of productions. While it offers some safety, it is a bit weird and in Farkle 7 the methods will be changed to just a `params` array of productions (and an `IEnumerable`). Passing an empty array to these methods could be detected by an analyzer.

Also the behavior of `SetProductions` will be changed to throw if called more than once on the same nonterminal, instead of ignoring subsequent calls.

[^any]: In Farkle 6 `.` matches any character that is not matched by any other at the present DFA state. This effectively gives `.` lower precedence than other patterns, which is something unusual for regexes.
