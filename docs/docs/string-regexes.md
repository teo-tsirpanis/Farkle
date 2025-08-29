# Regex pattern reference

In Farkle, terminals are defined by regular expressions or _regexes_. Defining a non-trivial regex used to take several lines of code like this example of a number with an optional sign at the beginning:

# [C#](#tab/csharp)

```csharp
using static Farkle.Builder.Regex;

Regex number = Concat(
    OneOf("+-").Optional(),
    OneOf(('0', '9'))
);
```

# [F#](#tab/fsharp)

``` fsharp
open Farkle.Builder.Regex

let number = concat [
    chars "+-" |> optional
    plus Number
]
```

---

Not anymore. Starting with Farkle 6, a regex can be defined much more simply and intuitively with a string. Here is the previous example, using a string regex:

# [C#](#tab/csharp)

``` csharp
using Farkle.Builder;

var number = Regex.FromRegexString("[+-]?\\d+");
```

# [F#](#tab/fsharp)

``` fsharp
open Farkle.Builder

let number = Regex.regexString "[+-]?\d+"
```

---

These regexes are regular objects of type `Regex`. They are composable, reusable and can be used anywhere instead of constructed regexes. Despite their similarity however, the language of regex strings is not the same with the language of popular regex libraries like PCRE or .NET's own `System.Text.RegularExpressions.Regex`. In this guide we will take a look at what is supported in regex strings, what isn't, and what is different.

## Supported constructs

### Character classes

In Farkle's string regexes, you can define character classes mostly in the same way with PCRE regexes Here's what is supported:

* You can define a regex that recognizes only one character —say `A`— surprisingly simply, by typing `A`.
* You can define a regex that recognizes only some characters —say `A`, `D`, `O` and `U`—, by typing `[ADOU]`. If you want your regex to match any character except of the four that were mentioned before, you can do that by typing `[^ADOU]`.
* You can define a regex that recognizes all characters in a range —say between `A` and `Z`—, by typing `[A-Z]`. Similarly, you can match all characters that don't lie between `A` and `Z` by typing `[^A-Z]`.
* You can combine the two previous rules and recognize multiple character sets and ranges on the same regex construct. For example you can match all valid Base64 characters (excluding the padding) by typing `[A-Za-z0-9+/]` and you can match all characters except of those that appear in valid Base64 by typing `[^A-Za-z0-9+/]`.
* Decimal digits can be matched by typing `\d`. All characters except of decimal digits can be matched by typing `\D`.
* Whitespace can be matched by typing `\s`. All characters except of whitespace can be matched by typing `\S`.
* You can match any character by typing `.`.
* In character sets and ranges you have to use `\` to escape the following characters: `-\]^`. For example, to match either left or the right brackets you have to type `[\[\]]`.
* Outside of character sets and ranges, you have to use `\` can be used to escape the following characters: `\.[{()|?*+`.
* The backslash character itself can be escaped with `\\`.

### Quantifiers

As with PCRE regexes, quantifiers like the `*`, `+` or `?` mean "zero or more", "one or more", and "zero or one" respectively. Less known quantifiers like `{m,n}`, `{m,}` and `{m}` mean "between `m` and `n` times", "at least `m` times" and "exactly `m` times" respectively.

### Precedence and grouping

The regex disjunction operator `|` takes precedence over regex concatenation, which means that `foo|bar` matches either `foo` or `bar`, not `fo`, either `o` or `b`, and then `ar`. You can specify a custom operator precedence with parentheses. For example, `fo(o|u)` matches only either `foo` or `fou`.

> [!NOTE]
> Parentheses exist only for defining operator precedence. Capturing groups is not supported.

### Matching behavior

Farkle does greedy matching. Lazy matching in the form of the `*?` and `+?` quantifiers is not supported.

### Escaping

When using `\` in regexes, be careful with the string escaping performed by programming languages themselves. To match a decimal digit, F# allows you to write an unrecognized escape sequence like `"\d"`, but C# doesn't, failing with an error and you have to use a verbatim string like `@"\d"`.

In a more complicated example, if you want to match the literal sequence of characters `\d`, the regex is either `'\d'` or `\\d`, which you would write as either `"'\\d'"` or `"\\\\d"`, or as either `@"'\d'"` or `@"\\d"` with a verbatim string.

### Shorthands inside character sets

Shorthands inside character sets are not currently supported. If you want to match all hexadecimal digits, you cannot write `[\da-fA-F]`; you have to write `[0-9a-fA-F]` instead.

### Unicode categories

Matching characters that belong to a Unicode category is not yet possible. Support might be added in a future version of Farkle if there is demand for it.
