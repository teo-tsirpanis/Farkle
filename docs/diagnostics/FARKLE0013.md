# FARKLE0013: Duplicate precompiler input method key

This error is emitted when two or more methods marked with @"Farkle.Builder.PrecompilerInputAttribute" in the same type, have the same value for the @"Farkle.Builder.PrecompilerInputAttribute.Key" property. It is also emitted if more than one input method in a type does not specify a key.

In order to fix it, ensure that each precompiler input method in a type has a unique key.

## Example code

```csharp
using Farkle.Builder;

// Non-compliant code: no input method specifies a key
public class MyPrecompilerInputs
{
    [PrecompilerInput]
    public static IGrammarBuilder<int> InputMethod1()
    {
        // ‥
    }

    [PrecompilerInput]
    public static IGrammarBuilder<int> InputMethod2()
    {
        // ‥
    }
}

// Non-compliant code: both methods specify the same key "GrammarA"
public class MyPrecompilerInputs
{
    [PrecompilerInput(Key = "GrammarA")]
    public static IGrammarBuilder<int> InputMethod1()
    {
        // ‥
    }

    [PrecompilerInput(Key = "GrammarA")]
    public static IGrammarBuilder<int> InputMethod2()
    {
        // ‥
    }
}

// Compliant code: each method specifies a unique key
public class MyFixedPrecompilerInputs
{
    [PrecompilerInput(Key = "GrammarA")]
    public static IGrammarBuilder<int> InputMethod1()
    {
        // …
    }

    [PrecompilerInput(Key = "GrammarB")]
    public static IGrammarBuilder<int> InputMethod2()
    {
        // …
    }
}
```
