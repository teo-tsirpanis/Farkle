---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0006
description: FARKLE0006: Operator defined multiple times
---
# FARKLE0006: Operator defined multiple times

This warning is emitted when a symbol in an `OperatorScope` is defined in multiple associativity groups. In this case, only the first definition will be used, and subsequent definitions will be ignored. Multiple occurrences of the same symbol in the same associativity group will not trigger this warning.

To fix this warning, ensure that each symbol is only defined in one associativity group.

## Example code

```csharp
IGrammarSymbol terminal1, terminal2, terminal3;
// Non-compliant code
OperatorScope operators = new OperatorScope(
    new LeftAssociative(terminal1, terminal2),
    new LeftAssociative(terminal1, terminal3)
);

// Compliant code
OperatorScope operators = new OperatorScope(
    new LeftAssociative(terminal1, terminal2),
    new LeftAssociative(terminal3)
);
```
