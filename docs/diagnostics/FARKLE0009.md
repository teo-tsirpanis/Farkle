# FARKLE0009: Regex is too complex

This error is emitted when Farkle fails to process a regex because it reached a limitation of the library or the system. In this case no DFA gets built and the grammar cannot be used for tokenizing.

The precise circumstances that trigger this error are an implementation detail, but encountering it is not expected to be common when working with real-world regexes.
