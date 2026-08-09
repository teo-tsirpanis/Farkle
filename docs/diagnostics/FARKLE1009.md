# FARKLE1009: Cannot infer types of production factory arguments

This warning is emitted when the production factory source generator fails to infer the type of some of the arguments to a call to `Production.Create`.

To understand when this situation can occur, consider the following grammar:

```csharp
[module: UseEnhancedSyntax]

var number = Terminals.Double("Number");

var function = Nonterminal.Create("Function",
    Production.Create("sqrt").FinishConstant(Math.Sqrt),
    Production.Create("sin").FinishConstant(Math.Sin));

var expression = Nonterminal.Create("Expression",
    Production.Create("nan").FinishConstant(double.NaN),
    Production.Create("infinity").FinishConstant(double.PositiveInfinity),
    Production.Create(function, number).Finish((f, n) => f(n)));

var statement = Nonterminal.Create("Statement",
    // The following line will emit FARKLE1009 as well as other compiler errors.
    Production.Create("print", expression).Finish(x => {
        Console.WriteLine(x);
        return (object?)null;
    }));
```

Before the production factory source generator runs, all calls to `Production.Create` are bound to a placeholder overload that accepts `params ReadOnlySpan<object>` and returns an untyped `ProductionBuilder`. Under this view, the third production of `Expression` has a syntax error, because `Production.Create(function, number)` returns `ProductionBuilder`, which does not have a `Finish` method that accepts a delegate with two parameters. This makes the compiler unable to infer the type of `expression`, so when we try to use it in the production of `Statement`, the source generator becomes unable to determine the type of `expression`, and does not generate a specific overload. This causes a mysterious syntax error on the `Finish` call, and we also emit FARKLE1009 to make it clearer what went wrong.

In this case, you can fix the problem by explicitly specifying the type of `expression`:

```csharp
[module: UseEnhancedSyntax]

var number = Terminals.Double("Number");

var function = Nonterminal.Create("Function",
    Production.Create("sqrt").FinishConstant(Math.Sqrt),
    Production.Create("sin").FinishConstant(Math.Sin));

IGrammarSymbol<double> expression = Nonterminal.Create("Expression",
    Production.Create("nan").FinishConstant(double.NaN),
    Production.Create("infinity").FinishConstant(double.PositiveInfinity),
    Production.Create(function, number).Finish((f, n) => f(n)));

var statement = Nonterminal.Create("Statement",
    Production.Create("print", expression).Finish(x => {
        Console.WriteLine(x);
        return (object?)null;
    }));
```

> [!TIP]
> Temporarily removing the `[UseEnhancedSyntax]` attribute might help you fix these problems, because it will give you the source generator's view of your code before it runs. In the example above, the use of the `expression` variable in the production of `Statement` will raise a [FARKLE1006](FARKLE1006.md) error with the message `Argument 1: cannot convert from '?' to 'string' or 'Farkle.Builder.IGrammarSymbol'`.
