# Farkle 7's tokenizer API

Since version 6.0 Farkle supports customizing the logic of breaking the input text into a sequence of tokens, known as _tokenizing_. This is used to implement parsers for languages that cannot be tokenized with a simple DFA, such as indentation-sensitive languages.

With Farkle 7's push-based parsing model, when a tokenizer needs to read more characters it can no longer just read them but it has to return and wait for the user to provide the characters. This presents the following challenges:

When parsing a token we would This writing tokenizers presents some unique challenges.

* When the parsing process is resumed, we want to support resuming the tokenizer at a specific point in its logic. Always restarting it from the end of the last token is both inefficient and impossible in some cases like noise groups where input gets consumed but no tokens are produced.

* Tokenizers could store information in the `ParserState` that would guide them to the right point when resuming, but this poses another problem: if we have many tokenizers, likely written by different people, how would these interact? In general, how would one tokenizer defer to another one? In Farkle 6 this was done by subclassing `DefaultTokenizer` and calling `base.Tokenize()`, but it won't work here, because inheritance is a very inflexible extension mechanism, and supporting resuming to tokenizers at arbitrary call depths is not practical. This means that tokenizers cannot call each other but need an external coordination mechanism.

* Even in cases of success a tokenizer would want to resume at a specific point at the next invocation (like if it wants to emit many tokens consecutively).
