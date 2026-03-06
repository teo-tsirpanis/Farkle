# FARKLE0010: Invalid usage of `PrecompilerInputAttribute`

This error is emitted when @"Farkle.Builder.PrecompilerInputAttribute" is applied on a method without meeting the usage requirements. In order to fix it, make sure that the method:

* Is `static`.
* Is not generic, or declared in a generic type.
* Has no parameters.
* Returns a type compatible with @"Farkle.Builder.IGrammarBuilder".
