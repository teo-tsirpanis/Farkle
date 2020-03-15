# Missing Features from GOLD Parser

When Farkle started being developed, it was an engine for GOLD Parser, as it still is. However, as it evolved, its architecture started diverging from the other engines. As a consequence, some of the lesser used and known things that GOLD Parser allowed are not allowed in Farkle. In this document, we are going to take a look at these things, and what to do about them.

So, are we ready? Let's do this!

## Virtual Terminals

When GOLD Parser's version 2.2 was released, back in 2004, one of its new features was the so-called "Virtual Terminals". So, what are these? A virtual terminal is a terminal that does not come from the engine's tokenizer, but from the user code that interacts with the engine. A connon use for virtual terminals is a parser for a language like Python. As you might have known, Python does not use keywords to define blocks of code, but instead uses indentation; the so-called "off-side rule". With virtual terminals, you could have written a grammar like this one:

```
"Start Symbol" = <Code Block>

IndentStart @= {Source = Virtual}
IndentStart = ' '
IndentEnd @= {Source = Virtual}
IndentEnd = ' '
Number = {Number}+

<Code Block> ::= IndentStart <Code Array> IndentEnd
<Code Array> ::= <Code> <Code Array> | <Code>
<Code> ::= <Numbers> | <Code Block>
```

This grammar is surprisingly simple. Its language consists of lists of numbers, which can be infinitely nested into sublists.

When you write the code to parse this grammar, you will create an `IndentStart` token every time you see that the indentation at the beginning has increased, and the other way around with `IndentEnd`.

Unfortunately, Farkle does not support virtual terminals. It's architecture does not allow you to generate tokens from a place different than the tokenizer.

However, as I was typing the above paragraph, I remembered something. Farkle's parser and tokenizer are completely decoupled, which allows you to write your own tokenizer which hooks into the real one.

Unfortunately, as I was trying to write a custom tokenizer for this grammar, I realized that it was extremely hard to make it correctly work. Farkle was not designed with virtual terminals in mind, yet I understand that this is an important feature to have. It _is_, __theoretically__ possible to do it, but practically cumbersome. If you have any solution to this problem, you can send it in a pull request and I would be happy to see it.

## Group End Symbols in Productions

The next GOLD Parser feature that is supported by Farkle is quite the bizarre one. But let's see an example first in another little grammar:

```
Comment Start = '/*'
Comment End = '*/'

"Start Symbol" = <S>

<S> ::= 'hello' | '/*'
```

The grammar's language is either the string `hello`, or the string `/*`, which also starts a comment. When you try to build this grammar in GOLD Parser, it will raise an error. The reason is surprisingly simple. If you parse a `/*`, the tokenizer will start a `Comment` group, unable to recognize that this is not what it sould do.

But if you replace the last line with this one?

```
<S> ::= 'hello' | '*/'
```

Instead of the comment start symbol, we 've got a comment _end_ symbol. Surprisingly, GOLD Parser would now build the grammar without a problem. And when we parse the string `*/`, the tokenizer would be able to recognize it - if we are outside of a comment of course - and will happily parse it. Hooray!

But Farkle, unfortunately, is not feeling the same. Historically, all GOLD Parser engines had one object type called symbol. This symbol, could be a terminal, a nonterminal, a noise symbol, a group starting symbol, a group ending symbol, and other things that did not make any sense. For example, the lexical error was represented by actual tokens that had a special symbol on their own, and so did the end of input.

While this approach was kept things simple, it was extremely type-unsafe. It allowed for example tokenizers to produce nonterminals, which does not happen in reality.

That's why Farkle took a much more strongly-typed approach starting from version 4.0.0, when the grammar types were rewritten from scratch without any inspiration from other GOLD Parser engines. After that, a tokenizer can _only_ output a terminal, a noise symbol and a group start/end symbol. Similarly, productions are _only_ made of a nonterminal head symbol and a handle of terminals and nonterminals, a transformer can be declared only for terminals, and so on. Any EGT file that tried to do something else was considered invalid. This change was definitely the best option, and paved the way for much safer and expressive code.

Now, as I have said before, starting from Farkle 4.0.0, a production's handle is made of a list of terminals and nonterminals. Allowing group end symbols in this closed group would significantly alter Farkle's architecture and would pose some new problems, like whether to make transformers support group end symbols as well. And allowing them would seem asymmetrical, given that group start symbols are understandably not allowed.

In the end, group end symbols are confined to their ascribed fate: to end groups. Any EGT file that has productions with group end symbols will be considered invalid, and a special error will be generated to explain this unfortunate situation.

---

So, I hope you enjoyed this little document. If you did, don't forget to give Farkle a try, and maybe you feel especially GOLD today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!
