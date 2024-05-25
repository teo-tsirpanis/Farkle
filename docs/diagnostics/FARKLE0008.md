---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0008
description: FARKLE0008: Symbol renamed more than once
---
# FARKLE0008: Symbol renamed more than once

This warning is emitted when a symbol is renamed multiple times with different names in the same grammar. When a renamed symbol gets encountered in the grammar, Farkle will make sure that its renamed name will be used in the grammar file and all diagnostic messages, but if the symbol is renamed multiple times, it is not certain which is the right name to use, and Farkle will emit this warning. Building the grammar will still succeed, and the symbol's renamed name that prevailed will be unspecified.

To fix this warning, ensure that each symbol is only renamed to one name.

## Example code

```csharp
IGrammarSymbol terminal;
// Non-compliant code
// terminal's name will be "Renamed 1" or "Renamed 2"
IGrammarSymbol nonterminal = Nonterminal.CreateUntyped("My Nonterminal",
    terminal.Appended().Append("x"),
    terminal.Rename("Renamed 1").Appended().Append("y"),
    terminal.Rename("Renamed 2").Appended().Append("z")
);

// Compliant code
// terminal's name will always be "Renamed 1"
IGrammarSymbol nonterminal = Nonterminal.CreateUntyped("My Nonterminal",
    terminal.Appended().Append("x"),
    terminal.Rename("Renamed 1").Appended().Append("y"),
    terminal.Rename("Renamed 1").Appended().Append("z")
);
```
