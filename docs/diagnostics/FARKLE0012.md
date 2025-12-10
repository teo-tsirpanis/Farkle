# FARKLE0012: Invalid parser return type of precompiler output method

This error is emitted when a method marked with @"Farkle.Builder.PrecompilerOutputAttribute" returns a @"Farkle.CharParser`1" whose type parameter is not allowed, as determined by the options set in the attribute, and the return type of the corresponding [input method](xref:Farkle.Builder.PrecompilerInputAttribute).

The @"Farkle.CharParser`1"'s type parameter must follow these rules in order to be valid:

* If the corresponding input method returns a type compatible with @Farkle.Builder.IGrammarBuilder`1", the grammar builder's type parameter must be compatible with the parser's type parameter.
  * This means that if the input method returns `IGrammarBuilder<string>`, the output method can return `CharParser<object>`, but not the other way around.
* If either the corresponding input method does not return a type compatible with @Farkle.Builder.IGrammarBuilder`1", or the @"Farkle.Builder.PrecompilerOutputAttribute.SyntaxCheck?displayProperty=nameWithType" property is set to `true`, the parser's type parameter must be a reference type.
