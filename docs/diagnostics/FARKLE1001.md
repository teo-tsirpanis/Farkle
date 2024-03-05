---
category: Diagnostic codes
categoryindex: 3
title: FARKLE1001
description: FARKLE1001: The `AsIs()` extension methods are obsolete
---
# FARKLE1001: The `AsIs()` extension methods are obsolete

The `AsIs()` extension methods on `ProductionBuilder<T1>` and `IGrammarSymbol<T>`[^1] are used as a shortcut to create productions with one significant member with no further processing. In Farkle 7 the more aptly named `AsProduction()` group of methods was introduced. You can resolve the obsoletion warning by simply changing calls to `AsIs()` to `AsProduction()`.

[^1]: known as `DesigntimeFarkle<T>` before Farkle 7
