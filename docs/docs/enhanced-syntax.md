# Using enhanced syntax to write your grammars

The enhanced syntax APIs were added in version 7.1.0, allowing you to use Farkle more ergonomically. With the help of a source generator, these APIs can provide an experience that is not possible using standard C# language features.

Currently, the only enhanced syntax feature is the production builder factory methods, which allow you to define a production with a single call to `Production.Build` rather than chaining calls to the `Append` and `Extend` methods.

## How to enable

The source generators and other Roslyn components powering enhanced syntax are included in the `Farkle` package, so you don't need to install anything else in your project.

Enhanced syntax is enabled by applying the `[UseEnhancedSyntax]` attribute. Here's an example:

```csharp
using Farkle.Builder;

public static class MyGrammar
{
    [UseEnhancedSyntax]
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");

        return Nonterminal.Create("Add Expression",
            Production.Build(number, "+", number).Finish((a, b) => a + b));
    }
}
```

Inside the code under the scope of the attribute, the source generator looks for calls to `Production.Build(...)` and generates appropriate overloads that match the types of the production's members. For the code above, the generator will create the following overload:

```csharp
internal static partial class Production
{
    public static ProductionBuilder<T1, T2> Create<T1, T2>(IGrammarSymbol<T1> member1, string member2, IGrammarSymbol<T2> member3) => …;
}
```

Production factories support objects of all the types that can be passed to the `Append` and `Extend` methods of production builders; namely @"Farkle.Builder.IGrammarSymbol" and @"System.String". If the type of an argument implements @"Farkle.Builder.IGrammarSymbol`1", the generated overload will have a generic type parameter corresponding to that member, and the return type of the overload will be a generic `ProductionBuilder` with the same number of type parameters. If an argument is a string, it will be converted to a literal symbol by calling @"Farkle.Builder.Terminal.Literal(System.String)?displayProperty=nameWithType".

### Where to put the attribute

You can add `[UseEnhancedSyntax]` to any member or type that can contain code blocks, such as methods, properties, and types. The attribute will have effect on all code syntactically contained within the member or type it is applied to.

If you want to use enhanced syntax in top-level statements, you can write `[module: UseEnhancedSyntax]` at the beginning of the file. Note that unlike most module-level attributes, applying the attribute on the module will have effect only on code in the same file as the attribute's application.

If you don't add `[UseEnhancedSyntax]` when using an enhanced syntax API, you will get compile errors. A code fix is available to automatically add the attribute if it's missing.

> [!TIP]
> For optimal performance, you are recommended to apply the attribute to the narrowest scope possible.

> [!IMPORTANT]
> In `partial` types and members, the attribute has effect only on the parts it is applied to. Putting it on an unrelated partial declaration will not enable enhanced syntax for the rest of the type.

## Comparing the fluent and production builder APIs

The older fluent API looks like this:

```csharp
return Nonterminal.Create("Expression",
    expr.Extended().Append("+").Extend(expr).Finish((x1, x2) => x1 + x2));
```

The production builder API is shorter and easier to read, especially when your production has many members:

```csharp
return Nonterminal.Create("Expression",
    Production.Build(expr, "+", expr).Finish((x1, x2) => x1 + x2));
```

A code fix is provided to migrate uses of the fluent API to production builders. The fluent API remains available, and there are no plans to remove it.

## Troubleshooting

Farkle comes with a set of analyzers and code fixes to provide a smooth and guided experience when using enhanced syntax. If you see a warning or error with a code starting with `FARKLE`, you can learn more about it by clicking its help URL. Let's talk about some other issues you might run into:

### Troubleshooting types not being found

If you see errors that the `UseEnhancedSyntaxAttribute` or `Production` types cannot be found, this means that the source generator is not running in your project. Make sure you are using an updated version of the `Farkle` package, and that source generators are supported by your toolchain.

### Source generation caveats

When a source generator runs, it cannot see code that was added by other source generators. While this usually means that source-generated code cannot rely on other source generators, because the enhanced syntax generator inspects method bodies instead of signatures, it is more likely for code using enhanced syntax to rely on source-generated code and exhibit mysterious compile errors. One such case is documented in the [FARKLE1009](../diagnostics/FARKLE1009.md) warning help page.
