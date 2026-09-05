# Customizing your grammar

Farkle offers various configuration options that allow you to customize the behavior of your grammar at different levels. This document describes the available options, as well as how to use them.

## Options affecting individual symbols in the grammar

The following options can be set by using extension methods in the @"Farkle.Builder.GrammarSymbolExtensions" class. These methods will return a new @"Farkle.Builder.IGrammarSymbol" instance with the specified option set.

Usually, these options have a cumulative nature, meaning that if you set an option on a symbol multiple times, or from multiple different uses of the symbol across the grammar, all of these values will have effect.

### Special names

In Farkle, the names of grammar symbols are used for presentation purposes only, and duplicate names are allowed. Because of this, you cannot look up a symbol by name, which would be useful if you use a custom tokenizer.

For this reason, Farkle supports setting a _special name_ for a grammar symbol, which is an optional alternative name that is guaranteed to be unique within the grammar. You can add a special name to a grammar symbol by using the @"Farkle.Builder.GrammarSymbolExtensions.AddSpecialName(Farkle.Builder.IGrammarSymbol,System.String)" extension method.

A symbol can have multiple special names. You can use the @"Farkle.Grammars.IGrammarProvider.GetSymbolFromSpecialName(System.String,System.Boolean)" method to look up a symbol by its special name. If you build a grammar with multiple symbols that have the same special name, building will fail.

## Options affecting the entire grammar

The following options can be set by using extension methods in the @"Farkle.Builder.GrammarBuilderExtensions" class. These methods will return a new @"Farkle.Builder.IGrammarBuilder" instance with the specified option set.

@"Farkle.Builder.IGrammarBuilder" (and its generic counterpart) is the base interface of @"Farkle.Builder.IGrammarSymbol" and cannot be used to build a grammar directly. For this reason, you can only use these methods on the grammar's starting symbol, which is the symbol that will be used to build the grammar.

### Case sensitivity

By default, grammars built by Farkle are case-sensitive. You can make your grammar case-insensitive by using the @"Farkle.Builder.GrammarBuilderExtensions.CaseSensitive(Farkle.Builder.IGrammarBuilder,System.Boolean)" extension method. You can also use the @"Farkle.Builder.GrammarBuilderExtensions.CaseSensitive(Farkle.Builder.IGrammarBuilder,Farkle.Builder.CaseSensitivity)" extension method to further customize the behavior, for example to make only literals case-insensitive, while keeping the rest of the terminals case-sensitive.

This option does not have any effect on regexes whose case sensitivity was explicitly set.

### Automatic whitespace handling

By default, grammars built by Farkle automatically ignore whitespace in input text. You can disable this behavior by using the @"Farkle.Builder.GrammarBuilderExtensions.AutoWhitespace(Farkle.Builder.IGrammarBuilder,System.Boolean)" extension method.

The set of ignored whitespace characters depends on whether the @"Farkle.Builder.Terminal.NewLine" symbol is used anywhere in the grammar:

* If the new line symbol is _not_ used, the ignored whitespace characters are: space, horizontal tab, carriage return, and line feed.
* If the new line symbol _is_ used, the ignored whitespace characters are: space and horizontal tab. Ignoring unexpected new lines is controlled by [a different option](#automatic-ignoring-of-unexpected-new-lines).

### Automatic ignoring of unexpected new lines

As described above, when the @"Farkle.Builder.Terminal.NewLine" symbol is used anywhere in a grammar, new lines are not part of the ignored whitespace characters, but form their own terminal. However, to avoid the sudden behavior change, new line tokens in places that are not expected by the grammar will be ignored if whitespace is also ignored.

You can customize this behavior by using the @"Farkle.Builder.GrammarBuilderExtensions.NewLineIsNoisy(Farkle.Builder.IGrammarBuilder,System.Boolean)" extension method.

### Grammar name

You can set a name for your grammar by using the @"Farkle.Builder.GrammarBuilderExtensions.WithGrammarName(Farkle.Builder.IGrammarBuilder,System.String)" extension method. The grammar's name is used for presentation purposes only, in places such as the generated HTML pages. By default, the grammar's name is the name of its starting symbol.

You can use the @"Farkle.Grammars.GrammarInfo.Name" property to retrieve a grammar's name.

### Operator scope

You can set [operator precedence and associativity](quickstart.md#operator-precedence-and-associativity) for your grammar by using the @"Farkle.Builder.GrammarBuilderExtensions.WithOperatorScope(Farkle.Builder.IGrammarBuilder,Farkle.Builder.OperatorPrecedence.OperatorScope)" extension method.

### Parser table generation algorithm

Farkle supports two algorithms for generating the grammar's LR(1) parser tables:

* [LALR(1)](https://en.wikipedia.org/wiki/LALR_parser)
* [IELR(1)](https://www.sciencedirect.com/science/article/pii/S0167642309001191)

IELR(1) is the default algorithm, and provides the full expressive power of LR(1) grammars without being susceptible to mysterious conflicts that can occur with LALR(1), while generating only slightly larger tables.

If a grammar produces parser tables without conflicts (before considering operator precedence and associativity), LALR(1) and IELR(1) will produce identical tables. Using LALR(1) might still be useful for educational or diagnostic purposes. You can use the @"Farkle.Builder.GrammarBuilderExtensions.WithParserGenerationAlgorithm(Farkle.Builder.IGrammarBuilder,Farkle.Builder.ParserGenerationAlgorithm)" extension method to specify which algorithm to use.

### Comments

You can use the @"Farkle.Builder.GrammarBuilderExtensions.AddLineComment(Farkle.Builder.IGrammarBuilder,System.String)" and @"Farkle.Builder.GrammarBuilderExtensions.AddBlockComment(Farkle.Builder.IGrammarBuilder,System.String,System.String)" extension methods to specify line and block comments for your grammar. These comments will be automatically ignored by the tokenizer.

When the tokenizer sees the start of a line comment, it will ignore all characters until the end of the line or the end of the input text. The new line characters will be kept in the input stream, so that the tokenizer can subsequently match @"Farkle.Builder.Terminal.NewLine" symbols, if present in the grammar.

When the tokenizer sees the start of a block comment, it will ignore all characters up to and including the end of the block comment. Block comments are not nested, which means that multiple occurrences of the block start symbol will be terminated by one occurrence of the block end symbol.

### Additional noise symbols

You can use the @"Farkle.Builder.GrammarBuilderExtensions.AddNoiseSymbol(Farkle.Builder.IGrammarBuilder,System.String,Farkle.Builder.Regex)" extension method to specify custom noise symbols for your grammar. When the tokenizer matches the given regex to some input text, it will ignore the matched characters and continue tokenizing the rest of the input text.

If the regex can match the same string as a non-noise symbol, the builder will prefer the non-noise symbol. If the regexes of two noise symbols can match the same string, the builder will prefer an unspecified one.

## Options controlling the build process

The following options provide additional control over the grammar building process, without affecting the resulting parser's behavior in any way[^1].

When building a grammar from code, you can set these options by using the @"Farkle.Builder.BuilderOptions" class, and passing an instance of it to the `Build` extension method. When using [the precompiler](the-precompiler.md), you can set these options in properties in the @"Farkle.Builder.PrecompilerInputAttribute" of your grammar's precompiler input method.

> [!NOTE]
> This document describes only the options that are available in both classes, but there are other options available in only one of the two. Make sure to check the documentation of both classes for a complete list of available builder options.

### Maximum DFA states

Farkle uses a deterministic finite automaton (DFA) to match regexes to input text. While DFAs are usually efficient, there are certain regexes that can cause the DFA to grow exponentially in size, which can lead to long build times and high memory usage. By default, Farkle limits the number of DFA states to an unspecified number that grows linearly with the complexity of your regexes, catching most cases of exponential growth.

You can customize this limit by setting the @"Farkle.Builder.BuilderOptions.MaxTokenizerStates" or @"Farkle.Builder.PrecompilerInputAttribute.MaxTokenizerStates" property to a positive integer. To disable the limit, set it to @"System.Int32.MaxValue".

[^1]: An exception is when building fails; changing a builder option is allowed to cause building a grammar to fail where it was succeeding before, or vice versa.
