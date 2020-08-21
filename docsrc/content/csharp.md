# Using Farkle from C\#

Farkle is a library written in F#, but supporting the much more used C# is a valuable feature. Because these two languages are quite different, there is a more idiomatic API for C# users. In this tutorial, we will assume that you have read [the F# quickstart guide][fsharp].

> The API described here is available in F# as well.

## Building grammars

F# programs using Farkle use an operator-laden API to compose designtime Farkles. Because C# does not support custom operators, we can instead use a different API based on extension methods.

### Creating regexes

Regular expressions are created using [members of the `Regex` class][regex], which is well documented. Predefined sets are in the `PredefinedSets` class.

### Cerating & building designtime Farkles

The following table highlights the differences between the F# and C# designtime Farkle API.

|F#|C#|
|--|--|
|`terminal "X" (T (fun _ x -> x.ToString())) r`|`Terminal.Create("X", (position, data) => data.ToString(), r)`|
|`"S" ||= [p1; p2]`|`Nonterminal.Create("S", p1, p2)`|
|`!@ x`|`x.Extended()`|
|`!% x`|`x.Appended()`|
|`!& "literal"`|`"literal".Appended()`|
|`empty`|`ProductionBuilder.Empty`|
|`newline`|`Terminal.NewLine`|
|`x .>> y`|`x.Append(y)`|
|`x .>>. y`|`x.Extend(y)`|
|`x => (fun x -> MyFunc x)`|`x.Finish(x => MyFunc(x))`
|`x =% 0`|`x.FinishConstant(0)`|
|`RuntimeFarkle.build x`|`x.Build()`|

### Customizing designtime Farkles

To customize things like the case-sensitivity of designtime Farkles, there are some extension methods for them that reside in the `Farkle` namespace.

## Parsing

To parse text, there are some extension methods for runtime Farkles that reside in the `Farkle` namespace.

These functions return an F# result type that can nevertheless be used from C# like this:

``` csharp
var designtime = /*...*/;
var runtime = designtime.Build();
var result = runtime.Parse("foobar");

if (result.IsOk)
    Console.WriteLine("Success. Result: {0}", result.OkValue);
else
    Console.WriteLine("Failure. Error message: {0}", result.ErrorValue);
---

So, I hope you enjoyed this little guide. If you did, don't forget to give Farkle a try, and maybe you feel especially sharp today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[fsharp]: quickstart.html
[regex]: reference/farkle-builder-regex.html
