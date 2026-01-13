# FARKLE0007: LR state machine has conflicts

This error is emitted when there is an ambiguity in the grammar's syntax that would cause the parser to have multiple possible actions to take when encountering a given symbol at a given state. Farkle still produces a grammar in this case, but it cannot be used for parsing.

When using the precompiler, LR conflicts are [reported in an HTML page](../docs/the-precompiler.md#lr-conflict-reporting) by default, and this error is emitted only once per grammar with LR conflicts.

TODO: Add a guide on how to resolve conflicts?
