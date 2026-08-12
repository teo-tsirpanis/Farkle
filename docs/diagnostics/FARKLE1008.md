# FARKLE1008: Migrate to production factory API

This diagnostic is emitted when your code creates production builder objects using the fluent API based on the `Append` and `Extend` methods, and suggests using the source-generated production factory API. A code fix will be provided to automatically migrate to the new API.

## Example code

```csharp
// Before migration:
using Farkle.Builder;

public static class MyGrammar
{
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            number.Extended().Append("+").Extend(number).Finish((a, b) => a + b),
            number.Extended().Append("-").Extend(number).Finish((a, b) => a - b));
    }
}

// After migration:
using Farkle.Builder;

public static class MyGrammar
{
    [UseEnhancedSyntax] // Required attribute to use source-generated APIs
    public static IGrammarBuilder<int> Builder()
    {
        IGrammarSymbol<int> number = Terminals.Int32("Number");
        return Nonterminal.Create("Add Expression",
            Production.Create(number, "+", number).Finish((a, b) => a + b),
            Production.Create(number, "-", number).Finish((a, b) => a - b));
    }
}
```
