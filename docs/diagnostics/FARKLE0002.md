## FARKLKE0002: Cannot distinguish between symbols

This error is emitted when the builder detects that two or more symbols cannot always be distinguished from each other because there is a string that matches all of them. For example, the regexes `ab*` and `a+` cannot be distinguished from each other because the string `a` matches both of them.

Conflicting regexes make parsing non-deterministic and will cause the builder to produce a grammar with a special DFA that cannot be used for parsing but can be useful for diagnostic purposes. You can still parse text with this grammar by using a custom tokenizer.

Farkle resolves certain conflicts automatically, favoring regexes with fixed length over those whose length can grow infinitely. For example, a conflict between the regexes `a+` and `a{3}` on the string `aaa` will be resolved in favor of the latter.

Furthermore, if the regex has an alternation in its root, each alternative will be considered separately. For example, a conflict between the regexes `a+` and `a{3}|b+` on the string `aaa` will be resolved again in favor of the latter. The variable-sized second alternative will not have an effect on the resolution of the conflict.

In code, this error is represented by the `Farkle.Diagnostics.IndistinguishableSymbolsError` class. A build diagnostic listener can cast the message object of diagnostics of this code to `IndistinguishableSymbolsError` to get more information about the conflicting symbols.

Farkle will not emit this error multiple times for the same set of symbols.
