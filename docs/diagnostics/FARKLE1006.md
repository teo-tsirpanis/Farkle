# FARKLE1006: Invalid usage of `Farkle.Builder.Production` factory method

This error is emitted when you attempt to call a source-generated production builder factory method in an unsupported way. Examples of such invalid parameters include:

* The call contains a parameter whose type is not @"System.String", and does not implement @"Farkle.Builder.IGrammarSymbol".
* The call contains more than 16 parameters of a type that implements @"Farkle.Builder.IGrammarSymbol`1".
