---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0009
description: FARKLE0009: Failed to parse regular expression
---
# FARKLE0009: Failed to parse regular expression

This error is emitted when Farkle fails to parse a string regular expression that was passed in the `Regex.FromRegexString` method, or the `Regex.regexString` F# function. In this case no DFA gets built and the grammar cannot be used for tokenizing.

To fix this error, ensure that the regular expression string is valid. The error message will help you identify and fix the problem.

## Example code

```csharp
// Non-compliant code
Regex r1 = Regex.FromRegexString("[a-z");

// Compliant code
Regex r2 = Regex.FromRegexString("[a-z]");
```
