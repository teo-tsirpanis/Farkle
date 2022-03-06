# Farkle's string regexes

In Farkle, terminals are defined by regular expressions or _regexes_. Defining a non-trivial regex used to take several lines of code like this example of a number with an optional sign at the beginning:

``` fsharp
open Farkle.Builder.Regex

let number = concat [
    chars "+-" |> optional
    plus Number
]
```

Not anymore. Starting with Farkle 6, a regex can be defined much more simply and intuitively with a string. Here is the previous example, using a string regex:

``` fsharp
open Farkle.Builder

let number = Regex.regexString "[+-]?\d+"
```

And in C#:

``` csharp
using Farkle.Builder;

var number = Regex.FromRegexString("[+-]?\\d+");
```

These regexes are full-blown `Regex`-typed objects. They are composable, reusable and can be used anywhere instead of constructed regexes. Despite their similarity however, the language of regex strings is not the same with the language of popular regex libraries like PCRE or .NET's own `System.Text.RegularExpressions.Regex`. In this guide we will take a look at what is supported in regex strings, what isn't and what is different. So, are you ready? Let's do this!

## Supported string regex constructs

### Character classes

In Farkle's string regexes, you can define character classes mostly in the same way with PCRE regexes Here's what is supported:

* You can define a regex that recognizes only one character -say `A`- surprisingly simply, by typing `A`.
* You can define a regex that recognizes only some characters -say `A`, `D`, `O` and `U`-, by typing `[ADOU]`. If you want your regex to match any character except of the four that were mentioned before, you can do that by typing `[^ADOU]`.
* You can define a regex that recognizes all characters in a range -say between `A` and `Z`-, by typing `[A-Z]`. Similarly, you can match all characters that don't lie between `A` and `Z` by typing `[^A-Z]`.
* Since Farkle 6.2.0, you can combine the two previous rules and recognize multiple character sets and ranges on the same regex construct. For example you can match all valid Base64 characters (excluding the padding) by typing `[A-Za-z+/]` and you can match all characters except of those that appear in valid Base64 by typing `[^A-Za-z+/]`.
* You can define a regex that recognizes all characters in a predefined set -say `Katakana`- by typing `\p{Katakana}`. The predefined sets' names are the same in the [`Farkle.Builder.PredefinedSets` module](reference/farkle-builder-predefinedsets.html). Similarly you can match all characters except of Katakana by typing `\P{Katakana}`. Since Farkle 6.4.0, you can also use the prefedefined set's property name in addition to the GOLD Parser name. For example, you can match the `All Letters` predefined set by typing both `\p{All Letters}` and `\p{AllLetters}`.
* Decimal digits can be matched by typing `\d`. All characters except of decimal digits can be matched by typing `\D`.
* Whitespace can be matched by typing `\s`. All characters except of whitespace can be matched by typing `\S`. Carriage return, line feed, space and horizontal tab are considered whitespace.
* You can match any other character by typing `.`. Just be careful of [the caveats](#The-dot-regex).
* You can match a literal sequence of characters by enclosing them into single quotes. For example `'[ADOU].'` will literally match the seven characters inside the single quotes without treating them specially. A single quote can be escaped by typing `''`.

> __Note:__ Prior to Farkle 6.2.0, single quotes could be escaped with `\'`. After that version the regex parser was improved but some constructs like that are no longer possible to maintain unambiguity. `\` is not anymore specially treated in literal strings.

* In character sets and ranges you have to use `\` to escape the following characters: `-\]^`. For example, to match either left or the right brackets you have to type `[\[\]]`.
* The backslash character itself can be escaped with `\\`.

### Quantifiers

As with PCRE regexes, quantifiers like the `*`, `+` or `?` mean "zero or more", "one or more", and "zero or one" respectively. Less known quantifiers like `{m,n}`, `{m,}` and `{m}` mean "between `m` and `n` times", "at least `m` times" and "exactly `m` times" respectively.

You can also stack quantifiers; `\d{4}?` will match either four decimal digits or none.

> __Note:__ Prior to Farkle 6.2.0, the regex above did not work due to a bug; you had to write `(\d{4})?`.

### Precedence and grouping

The regex disjunction operator `|` takes precedence over regex concatenation, which means that `foo|bar` matches either `foo` or `bar`, not `fo`, either `o` or `b`, and then `ar`. You can specify a custom operator precedence with parentheses. For example, `fo(o|u)` matches only either `foo` or `fou`.

> __Note:__ Parentheses exist only for defining operator precedence. Capturing groups is not supported.

## Caveats

### The dot regex

When I was describing the `.` regex, I intentionally told it matches any _other_ character and not _any_ character. In other words, __the `.` regex is matched only if no other regex can be matched__. The difference is subtle but can have a difference in certain scenarios.

Let's take a look at a simple regex for a string enclosed in double quotes that also supports escaping them: `"(.|\")*"`.

> __Note:__ You will need additional escaping to write the above regex in code.

The dot in the above regex will be never matched to a double quote because it also can be matched to the double quote at the end which has a higher priority. In essence, the regex above is the equivalent to `"([^"]|\")*"`.

Now, what if we required the string to have at least one character? The regex would have turned into `"(.|\")+"`.

But the regex above would match strings like `""foo"`. The reason to this is actually surprisingly simple. Generally `x+` is equivalent to `xx*`, making the regex above equivalent to `"(.|\")(.|\")*"`. In `""foo"`, the first double quote is matched to the first double quote in the regex, the second one is matched to the regex's first dot, and the third is matched to the regex's final double quote. So if you want a regex that matches strings with at least one character you have to explicitly write `"([^"]|\")+"`.

### Whitespace

In Farkle's string regexes, you can have arbitrary whitespace everywhere except of literal strings and character sets and ranges. This means that `f o o ( bar ) ?` is equivalent to `foo(bar)?`. If you want to match a literal space you can escape the space (`' '`) or use a character set (`[ ]`).

This deliberate deviation from the typical regex syntax was made due to Farkle's philosophy that whitespace is automatically handled by default, and allows you to write big regexes in a more clean and less compact way.

### Escaping

When using `\` in regexes, be careful with the string escaping performed by programming languages themselves. To match a decimal digit, F# allows you to write an unrecognized escape sequence like `"\d"`, but C# doesn't, failing with an error and you have to use a verbatim string like `@"\d"`.

In a more complicated example, if you want to match the literal sequence of characters `\d`, the regex is either `'\d'` or `\\d`, which you would write as either `"'\\d'"` or `"\\\\d"`, or as either `@"'\d'"` or `@"\\d"` with a verbatim string.

Similarly, writing `"\n"` somewhere in a regex will be ignored because it is whitespace, as we saw earlier. If you want to match the literal sequence of characters `\n`, you would follow the example we saw in the previous paragraph. If you want to match an actual line feed character, you would either write it with a literal string as `"'\n'"`, or with a character set as `"[\n]"`.

### Unicode categories

Matching characters that belong in a Unicode category is not yet possible. Support might be added in a future version of Farkle if there is demand for it.

## How do they work

Finally, let's take a look at how string regexes work. It's actually surprisingly simple. These strings are parsed and converted to constructed regexes using Farkle itself. That parsing happens when you build a designtime Farkle containing a string regex. If a syntax error occurs in a regex string, building the designtime Farkle will fail.

You can parse strings into regular expressions yourself by using the objects in the [`Farkle.Builder.RegexGrammar` module](reference/farkle-builder-regexgrammar.html).

---

So I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try and maybe you feel especially quantified today and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!
