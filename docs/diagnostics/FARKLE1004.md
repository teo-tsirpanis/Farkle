---
category: Diagnostic codes
categoryindex: 3
title: FARKLE1004
description: FARKLE1004: Types have been renamed in Farkle 7
---
# FARKLE1004: Types have been renamed in Farkle 7

In Farkle 7, some types have been renamed to more intuitive names. Types with the old names are still available, but are marked as obsolete and using them will produce a compiler error. In order to use Farkle 7, you must update your code to use the new type names.

The following table lists the changes you need to make:

|Old name|New name|
|-|-|
|`Farkle.RuntimeFarkle<T>`|`Farkle.CharParser<T>`|
|`Farkle.Builder.DesigntimeFarkle`|`Farkle.Builder.IGrammarSymbol` / `Farkle.Builder.IGrammarBuilder` (1)|
|`Farkle.Builder.DesigntimeFarkle<T>`|`Farkle.Builder.IGrammarSymbol<T>` / `Farkle.Builder.IGrammarBuilder<T>` (1)|
|`Farkle.Builder.PrecompilableDesigntimeFarkle`|`Farkle.Builder.IGrammarBuilder` (2)|
|`Farkle.Builder.PrecompilableDesigntimeFarkle<T>`|`Farkle.Builder.IGrammarBuilder<T>` (2)|

In F#, using these types will not cause an error but you should still update your code.

The types with the old names will be completely removed in a future version of Farkle, and using them will produce a standard compiler error with no migration guidance.

## Notes

1. The `DesigntimeFarkle` type does not have a direct replacement. Instead, you should use `IGrammarSymbol` to store individual grammar symbols, and `IGrammarBuilder` to store the whole grammar to be built. The same applies to the types' generic counterparts.
2. The precompiler is not available in the first preview releases of Farkle 7. You must change references of `PrecompilableDesigntimeFarkle` to `IGrammarBuilder`, and when it becomes available, further guidance will be provided.
