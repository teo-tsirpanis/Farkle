---
category: Diagnostic codes
categoryindex: 3
title: FARKLE1002
description: FARKLE1002: The BuildUntyped extension method is obsolete
---
# FARKLE1002: The BuildUntyped extension method is obsolete

The `BuildUntyped` extension method on `IGrammarBuilder`[^1] is used to build a parser with a semantic provider that always returns `null`. Because the method's name does not clearly indicate the lack of semantic analysis, Farkle 7 introduced the `BuildSyntaxCheck` extension method was introduced with the same behavior. You can resolve the obsoletion warning by simply changing calls from `BuildUntyped` to `BuildSyntaxCheck`.

If you want to build an untyped `IGrammarBuilder` and keep its semantic actions, you can use the `Cast` extension method to convert it to an `IGrammarBuilder<object?>`, and then build it with `Build`.

[^1]: known as `DesigntimeFarkle<T>` before Farkle 7
