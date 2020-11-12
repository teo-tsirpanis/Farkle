# Farkle's string regexes

In Farkle, terminals are defined by regular expressions or _regexes_. Defining a non-trivial regex used to take several lines of code like this example of a number with an optional sign at the beginning:

``` fsharp
open Farkle.Builder

let number = concat [
    chars "+-" |> optional
    plus Number
]
```

Not anymore. Starting with Farkle 6, a regex can be defined much more simply and intuitively with a string. Here is the previous example, using a string regex:

``` fsharp
open Farkle.Builder

let number = regexString "[+-]?\d+"
```

And in C#:

``` csharp
using Farkle.Builder;

var number = Regex.FromRegexString("[+-]?\d+");
```

These regexes are full-blown `Regex`-typed objects. They are composable, reusable and can be used anywhere instead of constructed regexes. Despite their similarity however, the language of regex strings is not the same with the language of popular regex libraries, say PCRE. In this guide we will take a look at what is supported in regex strings and what isn't. So, are you ready? Let's do this!

## Supported string regex constructs

### Character classes

In Farkle's string regexes, you can define character classes mostly in the same way with PCRE regexes Here's what is supported:

* You can define a regex that recognizes only one character -say `A`- surprisingly simply, by typing `A`.
* You can define a regex that recognizes only some characters -say `A`, `D`, `O` and `U`-, by typing `[ADOU]`. If you want your regex to match any character except of the four that were mentioned before, you can do that by typing `[^ADOU]`.
* You can define a regex that recognizes all characters in a range -say between `A` and `Z`-, by typing `[A-Z]`. Similarly, you can match all characters that don't lie between `A` and `Z` by typing `[^A-Z]`.
* You can define a regex that recognizes all characters in a predefined set -say `Katakana`- by typing `\p{Katakana}`. The predefined sets' names are the same in the `Farkle.Builder.PredefinedSets` module. Similarly you can match all characters except of Katakana by typing `\P{Katakana}`.
* Decimal numbers can be matched by typing `\d`. All characters except of decimal numbers can be matched by typing `\D`.
* Whitespace can be matched by typing `\s`. All characters except of whitespace can be matched by typing `\S`. Carriage return, line feed, space and horizontal tab are considered whitespace.
* You can match any other character by typing `.`. __Just be careful of the caveats mentioned further down the guide.__
* You can match a literal sequence of characters by enclosing them into single quotes. For example `'[ADOU].'` will literally match the seven characters inside the single quotes without treating them specially. A single quote inside a literal sequence can be inserted by typing `\'`. A single quote outside of a literal sequence can be inserted by typing `''`.
* In character sets and ranges you can escape a character with a `\`. For example, to match either left or the right brackets you have to type `[\[\]]`.

### Quantifiers

As with PCRE regexes, quantifiers like the `*`, `+` or `?` mean "zero or more", "one or more", and "zero or one" respectively. Less known quantifiers like `{m,n}`, `{m,}` and `{m}` mean "between `m` and `n` times", "at least `m` times" and "exactly `m` times" respectively.

### Precedence and grouping

The regex disjunction operator `|` takes precedence over regex concatenation, which means that `foo|bar` matches either `foo` or `bar`, not `fo`, either `o` or `b`, and then `ar`. You can specify a custom operator precedence with parentheses. For example, `fo(o|u)` matches only either `foo` or `fou`.

> __Note__: Parentheses exist only for defining operator precedence. Capturing groups is not supported.

## Caveats

### The dot regex

When I was describing the `.` regex, I intentionally told it matches any _other_ character and not _any_ character. In other words, __the `.` regex is matched only if no other regex can be matched__. The difference is subtle but can have a difference in certain scenarios.

Let's take a look at a simple regex for a string enclosed in double quotes: `"(.|\")*"`.

> __Note__: You will need additional escaping to write the above regex in code.

The dot in the above regex will be never matched to a double quote because it also can be matched to the double quote at the end which has a higher priority. In essence, the regex above is the equivalent to `"([^"]|\")*"`.

Now, if we required the string to have at least one character? The regex would have turned into `"(.|\")+"`.

But the regex above would match regexes like `""foo"`. The reason to this is actually surprisingly simple. Generally `x+` is equivalent to `xx*`, making the regex above equivalent to `"(.|\")(.|\")*"`. In `""foo"`, the first double quote is matched to the first double quote in the regex, the second one is matched to the regex's first dot, and the third is matched to the regex's final double quote. So if you want a regex that matches strings with at least one character you'd better write `"([^"]|\")+"`.

### Whitespace

In Farkle's string regexes, you can have arbitrary whitespace everywhere except of literal strings and character sets and ranges. This means that `f o o ( bar ) ?` is equivalent to `foo(bar)?`. If you want to match a literal space you can use a literal string (`' '`) or a character set (`[ ]`).

### Unicode categories

Matching characters that belong in a Unicode category is not yet possible. Support _might_ be added in a future version of Farkle.

## How do they work

Finally, let's take a look at how string regexes work. It's actually surprisingly simple. These strings are parsed and converted to constructed regexes using Farkle itself. That parsing happens when you build a designtime Farkle containing a string regex. If a syntax error occurs in a regex string, building the designtime Farkle will fail.

---

So I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try and maybe you feel especially quantified today and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!