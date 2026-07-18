# FARKLE1005: This API requires `[UseEnhancedSyntax]` attribute

This error is emitted when you attempt to use an API that requires the `Farkle.Builder.UseEnhancedSyntaxAttribute` attribute. One example of such an API is the source-generated production factory methods.

You can resolve the error by adding the `[UseEnhancedSyntax]` attribute to a member containing the code that is using the API.

## Example code

```csharp
using Farkle.Builder;

public static class MyGrammar
{
    // Non-compliant code: the production factory method requires the [UseEnhancedSyntax] attribute
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Create(number, "+", number, (a, b) => a + b));
    }
}

// Compliant code: the [UseEnhancedSyntax] attribute is applied to the method
public static class MyGrammar
{
    [UseEnhancedSyntax]
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Create(number, "+", number, (a, b) => a + b));
    }
}

// Compliant code: the [UseEnhancedSyntax] attribute is applied to the containing type
[UseEnhancedSyntax]
public static class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Create(number, "+", number, (a, b) => a + b));
    }
}
```
