---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0005
description: FARKLE0005: Nonterminal was not set productions
---
# FARKLE0005: Nonterminal was not set productions

This warning is emitted when a recursive nonterminal was created, but its productions were never set with the `SetProductions` method. This method allows nonterminals to have with productions that contain the nonterminals themselves, and not calling it will result in a nonterminal that can never be produced. This will subsequently result in unexpected syntax errors when parsing text with the grammar.

To fix this warning, make sure you have called `SetProductions` on the nonterminal before building a grammar that contains it.

## Example code

```csharp
// Non-compliant code
Nonterminal<int> myNonterminal = Nonterminal.Create<int>("MyNonterminal");

// Compliant code
Nonterminal<int> myNonterminal = Nonterminal.Create<int>("MyNonterminal");
myNonterminal.SetProductions(
    // …
);
```
