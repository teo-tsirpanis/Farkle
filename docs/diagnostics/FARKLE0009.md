---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0009
description: FARKLE0009: Failed to parse regular expresssion
---
# FARKLE0009: Failed to parse regular expresssion

This error is emitted when Farkle fails to parse a string regular expression that was passed in the `Regex.FromRegexString` method, or the `Regex.regexString` F# function.

When this error is emitted, the string regex will be substituted with [a regex that matches nothing](FARKLE0003.md), and the parser built by Farkle will always fail to parse any text. The grammar will still be generated, but directly using it for tokenizing will very likely result in unexpected behavior.

To fix this error, ensure that the regular expression string is valid. The error message will help you identify and fix the problem.

## Example code

```csharp
// Non-compliant code
Regex r1 = Regex.FromRegexString("[a-z");

// Compliant code
Regex r2 = Regex.FromRegexString("[a-z]");
```
