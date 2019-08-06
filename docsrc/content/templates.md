# Farkle Templating reference

As you might have seen in the previous tutorials, Farkle comes with a helpful command line tool to help you create source code files of your grammar. Here I will tell you how this tool works, and how to extend this tool even further for your own needs.

> Before we begin, this reference document is quite advanced, and most of what is says will not be useful to the 90% of the users of Farkle.

So, are we ready? Let's do this!

---

To create these beautiful source files, Farkle uses [Scriban][scriban]. Scriban is a library that generates text templates. To achieve this, it uses a surprisingly simple, yet incredibly powerful templating language which I recommend you to learn from [the official documentation][scriban-doc]. Once you 've learned it, let's see how Farkle integrates with it.

---

To integrate with Scriban, external libraries like Farkle expose certain veriables and functions to Scriban templates. The variables exposed by Farkle are the following:

* `farkle`: An object containing information about Farkle itself.

  * `farkle.version`: The version of the tool used to grnerate the template.

* `grammar`: An objct describing the grammar that was given. It is not a full-blown object of type `Farkle.Grammar.Gramamr`, but a cut-down object, because of [problems of Scriban][scriban-issue-151]. If you want extra functionality to this object, fell free to open an issue.

  * `grammar.properties`: A key-value pair of the grammar's metadata. See more at the [documentation of GOLD Parser][gold-properties].

  * `grammar.symbols.terminals`: An array of the terminal symbols of the grammar.

  * `grammar.symbols.nonterminals`: An array of the nonterminal symbols of the grammar.

  * `grammar.symbols.noise_symbols`: An array of the noise symbols of the grammar.

  * `grammar.productions`: An array of the productions of the grammar.

> __Note:__ Terminals, nonterminals and productions have an `index` property which returns their arithmetic index in the symbol/production table.

* `grammar_path`: The path of the grammar file that was given.

* `namespace`: The namespace of the generated source file. Defaults to the grammar's name. You can change it in the command line tools with the `--namespace` option.

* `to_base_64 <bool>`: Returns the EGT file as a Base64-encoded string. If you pass `true`, it will add line breaks every 76 characters.

* `fmt <terminal or production> <case> <separator>`: Formats a symbol or a production into a string. You can specify the case of the generated string (`upper_case`, `lower_case`, `pascal_case`, `camel_case`), as well as the separator between the members of the production.

## Using the custom template

After you have written your template, you can use it by running `farke new -t MyWonderfulTemplate.scriban MyMarvelousGrammar.egt`.

[scriban]: https://github.com/lunet-io/scriban
[scriban-doc]: https://github.com/lunet-io/scriban/blob/master/doc/language.md
[scriban-issue-151]: https://github.com/lunet-io/scriban/issues/151
[gold-properties]: http://www.goldparser.org/doc/grammars/define-properties.htm
