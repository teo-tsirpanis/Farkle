## FARKLE0001: DFA exceeds maximum number of states

Certain regular expressions can result in a DFA that has a disproportionately large number of states. For example the regex `[ab]*a[ab]{32}` will result in a DFA with billions of states. In previous versions of Farkle, such regexes would cause the builder to consume large amounts of memory. Starting with Farkle 7 there is a limit on the number of states that a DFA can have. This error is emitted when that limit is reached, in which case no DFA gets built.

This limit can be customized by setting the `BuilderOptions.MaxTokenizerStates` property. Setting it to `int.MaxValue` will disable the limit.

TODO: Add an example when the relevant APIs get implemented.

The default value is an implementation detail and linearly scales with the complexity of the regexes being built, thus avoiding exponential state blowups.
