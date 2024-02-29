---
category: Diagnostic codes
categoryindex: 3
title: FARKLE0003
description: FARKLE0003: Regex cannot be matched by any character
---
# FARKLE0003: Regex cannot be matched by any character

This warning is emitted when the builder detects that a regex or a part of it cannot be matched by any character.

This can occur when a regex contains constructs like `Regex.Choice([])`, Regex.OneOf([])`, or `Regex.NotOneOf([char.MinValue, char.MaxValue])`. Earlier versions of Farkle were automatically converting these pathological cases to `Regex.Empty` which can match the regex string[^1], but Farkle 7 prefers to stick to their formal meaning and have them match _nothing_.

Regexes that match nothing have no practical use case. If you are getting this warning you should check the regex for that symbol and make sure it does not contain expressions that match no characters.
