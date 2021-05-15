# Using Farkle from C\#

Farkle is a library written in F#, but supporting the much more used C# is a valuable feature. Because these two languages are quite different, there is a more idiomatic API for C# users. In this tutorial, we will assume that you have read [the F# quickstart guide][fsharp].

> The API described here is available in F# as well.

## Building grammars

F# programs using Farkle use an operator-laden API to compose designtime Farkles. Because C# does not support custom operators, we can instead use a different API based on extension methods.

### Creating regexes

Regular expressions are created using [members of the `Regex` class][regex], which is well documented. Predefined sets are in the [`PredefinedSets`][predefinedsets] class. Let's see a comparison:

``` fsharp
open Farkle.Builder
open Farkle.Builder.Regex

let regex1 = regexString "Hello+' '?World!*"

let regex2 = concat [
    string "Hell"
    char 'o' |> plus
    char ' ' |> optional
    string "World"
    char '!' |> star
]
```

``` csharp
using Farkle.Builder;
using static Farkle.Builder.Regex;

var regex1 = FromRegexString("Hello+' '?World!*");

var regex2 = Join(
    Literal("Hell"),
    Literal('o').AtLeast(1),
    Literal(' ').Optional(),
    Literal("World"),
    Literal('!').ZeroOrMore()
);
```

### Cerating & building designtime Farkles

The following table highlights the differences between the F# and C# designtime Farkle API.

|F#|C#|
|--|--|
|`terminal "X" (T (fun _ x -> x.ToString())) r`|`Terminal.Create("X", (_, x) => x.ToString(), r)`|
|`"S" ||= [p1; p2]`|`Nonterminal.Create("S", p1, p2)`|
|`!@ x`|`x.Extended()`|
|`!% x`|`x.Appended()`|
|`!& "literal"`|`"literal".Appended()`|
|`empty`|`ProductionBuilder.Empty`|
|`newline`|`Terminal.NewLine`|
|`x .>> y`|`x.Append(y)`|
|`x .>>. y`|`x.Extend(y)`|
|`x |> asIs`|`x.AsIs()`|
|`x => (fun x -> MyFunc x)`|`x.Finish(x => MyFunc(x))`
|`x =% 0`|`x.FinishConstant(0)`|
|`RuntimeFarkle.build x`|`x.Build()`|
|`RuntimeFarkle.buildUntyped x`|`x.BuildUntyped()`|

The `Build` and `BuildUntyped` extension methods accept an optional `CancellationToken` and will throw an `OperationCanceledException` if it gets triggered.

### A complete example

Let's take a look at [the calculator we made at the quick start guide](quickstart.html#Writing-more-complex-nonterminals) written in C#:

``` csharp
using System;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;

public static class SimpleMaths
{
    public static readonly DesigntimeFarkle<double> Designtime;
    public static readonly RuntimeFarkle<double> Runtime;

    static SimpleMaths()
    {
        var number = Terminals.Double("Number");

        var expression = Nonterminal.Create<double>("Expression");
        expression.SetProductions(
            number.AsIs(),
            expression.Extended().Append("+").Extend(expression).Finish((x1, x2) => x1 + x2),
            expression.Extended().Append("-").Extend(expression).Finish((x1, x2) => x1 - x2),
            expression.Extended().Append("*").Extend(expression).Finish((x1, x2) => x1 * x2),
            expression.Extended().Append("/").Extend(expression).Finish((x1, x2) => x1 / x2),
            "-".Appended().Extend(expression).WithPrecedence(out var NEG).Finish(x => -x),
            expression.Extended().Append("^").Extend(expression).Finish(Math.Pow),
            "(".Appended().Extend(expression).Append(")").AsIs());

        var opScope = new OperatorScope(
            new LeftAssociative("+", "-"),
            new LeftAssociative("*", "/"),
            new PrecedenceOnly(NEG),
            new RightAssociative("^"));

        Designtime = expression.WithOperatorScope(opScope);
        Runtime = Designtime.Build();
    }
}
```

Notice how we called the `WithPrecedence` method. In F# we were passing an object to the `prec` function. In C# we let the method create and return that object to us, taking advantage of C# 7.0's `out var` construct. We can still pass an object if we want.

### Customizing designtime Farkles

To customize things like the case-sensitivity of designtime Farkles, there are some extension methods for them that reside in the `Farkle.Builder` namespace. Let's take a look at an example:

``` csharp
var customized =
    SimpleMaths.Designtime
        .AddBlockComment("/*", "*/")
        .AddLineComment("//")
        .AutoWhitespace(false)
        .CaseSensitive(false)
        .MarkForPrecompile()
        .Build();
```

## Parsing

To parse text, there are some extension methods for runtime Farkles that reside in the `Farkle` namespace. These functions return an F# result type that can nevertheless be used from C# like this:

``` csharp
var designtime = /*...*/;
var runtime = designtime.Build();
// Parsing strings.
var result = runtime.Parse("foobar");

if (result.IsOk)
    Console.WriteLine("Success. Result: {0}", result.OkValue);
else
    Console.WriteLine("Failure. Error message: {0}", result.ErrorValue);

// Parsing ReadOnlyMemories
runtime.Parse("foobar".AsMemory());
// Parsing TextReaders
using (var f = File.OpenText("foobar.txt"))
{
    runtime.Parse(f);
}
// Parsing files
runtime.ParseFile("foobar.txt");
```

---

So, I hope you enjoyed this little guide. If you did, don't forget to give Farkle a try, and maybe you feel especially sharp today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[fsharp]: quickstart.html
[regex]: reference/farkle-builder-regex.html
[predefinedsets]: reference/farkle-builder-predefinedsets.html
