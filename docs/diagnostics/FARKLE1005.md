# FARKLE1005: This API requires `[UseEnhancedSyntax]` attribute

This error is emitted when you attempt to use an API that requires the `Farkle.Builder.UseEnhancedSyntaxAttribute` attribute. One example of such an API is the source-generated production builder factory methods.

You can resolve the error by adding the `[UseEnhancedSyntax]` attribute to a member containing the code that is using the API.

> [!IMPORTANT]
> In partial types and members, the attribute will have effect only in code on the parts it is applied to.

## Example code

```csharp
using Farkle.Builder;

// Non-compliant code: the production builder factory method requires the [UseEnhancedSyntax] attribute
public static class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Build(number, "+", number).Finish((a, b) => a + b));
    }
}

// Non-compliant code: the [UseEnhancedSyntax] attribute is applied to an unrelated part of the type
public static partial class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Build(number, "+", number).Finish((a, b) => a + b));
    }
}
[UseEnhancedSyntax]
public static partial class MyGrammar;

// Compliant code: the [UseEnhancedSyntax] attribute is applied to the method
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

// Compliant code: the [UseEnhancedSyntax] attribute is applied to the containing type
[UseEnhancedSyntax]
public static class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Build(number, "+", number).Finish((a, b) => a + b));
    }
}
```
