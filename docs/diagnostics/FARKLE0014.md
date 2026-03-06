# FARKLE0014: Precompiler output method key not found

This error is emitted when a method marked with @"Farkle.Builder.PrecompilerOutputAttribute" specifies a @"Farkle.Builder.PrecompilerOutputAttribute.Key" property, but no [precompiler input method](xref:Farkle.Builder.PrecompilerInputAttribute) with a matching key was found in the same type. It is also emitted if a precompiler output method does not specify a key, but there isn't a precompiler input method in the same type that also does not specify a key.

In order to fix it, ensure that each precompiler output method specifies a key that matches a precompiler input method's key in the same type.

## Example code

```csharp
using Farkle;
using Farkle.Builder;

// Non-compliant code: output method's key "GrammarA" does not match any input method's key
public class MyPrecompilerMethods
{
    [PrecompilerInput(Key = "GrammarA")]
    public static IGrammarBuilder<int> InputMethod()
    {
        // ‥
    }

    [PrecompilerOutput(Key = "GrammarB")]
    public static CharParser<int> OutputMethod() => CharParser.MustPrecompile<int>();
}

// Compliant code: output method's key "GrammarA" matches input method's key
public class MyFixedPrecompilerMethods
{
    [PrecompilerInput(Key = "GrammarA")]
    public static IGrammarBuilder<int> InputMethod()
    {
        // ‥
    }

    [PrecompilerOutput(Key = "GrammarA")]
    public static CharParser<int> OutputMethod() => CharParser.MustPrecompile<int>();
}
```
