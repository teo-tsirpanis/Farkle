---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0007
description: FARKLE0007: LR state machine has conflicts
---
# FARKLE0007: LR state machine has conflicts

This error is emitted when there is an ambiguity in the grammar's syntax that would cause the parser to have multiple possible actions to take when encountering a given symbol at a given state. Farkle still produces a grammar in this case, but it cannot be used for parsing.

TODO: Add a guide on how to resolve conflicts?
