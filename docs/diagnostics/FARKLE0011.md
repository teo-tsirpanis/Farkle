# FARKLE0011: Invalid usage of `PrecompilerOutputAttribute`

This error is emitted when @"Farkle.Builder.PrecompilerOutputAttribute" is applied on a method without meeting the usage requirements. In order to fix it, make sure that the method:

* Is `static`.
* Is not generic, or declared in a generic type.
* Has no parameters.
* Returns one of the following types:
  * @"Farkle.Grammars.Grammar"
  * @"Farkle.CharParser`1"
