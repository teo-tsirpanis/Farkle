# FARKLE1007: Unnecessary use of `[UseEnhancedSyntax]` attribute

This diagnostic is emitted when the `[UseEnhancedSyntax]` attribute is applied to a type or member that does not contain any uses of an API that requires the attribute.

You can resolve the diagnostic by removing the reported application of the `[UseEnhancedSyntax]` attribute.

## Example code

```csharp
// Non-compliant code: the [UseEnhancedSyntax] attribute is applied to a method that uses the fluent API to build productions
public static class MyGrammar
{
    [UseEnhancedSyntax]
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            number.Extended().Append("+").Extend(number).Finish((a, b) => a + b));
    }
}

// Compliant code: the [UseEnhancedSyntax] attribute was removed
public static class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            number.Extended().Append("+").Extend(number).Finish((a, b) => a + b));
    }
}

// Compliant code: the code has migrated to use source-generated production factory methods, so the  [UseEnhancedSyntax] attribute is now required
public static class MyGrammar
{
    [UseEnhancedSyntax]
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Create(number, "+", number).Finish((a, b) => a + b));
    }
}
```
