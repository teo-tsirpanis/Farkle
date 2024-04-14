---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0004
description: FARKLE0004: Duplicate special name definition
---
# FARKLE0004: Duplicate special name definition

This error is emitted when the builder detects that multiple symbols in the grammar have the same special name. Special names are used to uniquely identify symbols in the grammar to allow later using them as part of a custom tokenizer for example.

To allow this unique identification, Farkle does not allow duplicate special names to be defined in a grammar. If such a case is detected this error is emitted and the builder will proceed with building the grammar, but with a special flag that marks it as unsuitable for parsing, and without emitting a special names table.

This error is likely to occur in grammars where some of its symbols are obtained from reusable libraries. You can make it less likely to occur by using long and unique special names, and then rename the symbols to a more user-friendly name.

## Example code

```csharp
// This will emit FARKLE0004 if another terminal has a special name of "MyTerminal".
IGrammarSymbol terminal = Terminal.Virtual("MyTerminal", TerminalOptions.SpecialName);

// This is less likely to emit FARKLE0004. You might also want to let the user provide
// a unique prefix and add to the special name to further reduce the chances of a
// collision.
IGrammarSymbol terminal =
    Terminal.Virtual("__MyCompany_MyLibrary_MyTerminal", TerminalOptions.SpecialName)
        .Rename("MyTerminal");
```
