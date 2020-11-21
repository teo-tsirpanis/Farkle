# Templating Reference

## This feature currently works only with GOLD Parser grammars from EGT files. A future version of Farkle will support precompiled grammars.

As you might have seen in the previous tutorials, Farkle comes with a helpful command line tool to help you create source code files of your grammar. Here I will tell you how this tool works, and how to extend this tool even further for your own needs.

> Before we begin, this reference document is quite advanced, and most of what is says will not be useful to the 90% of the users of Farkle.

So, are we ready? Let's do this!

---

To create these beautiful source files, Farkle uses [Scriban][scriban]. Scriban is a library that generates text templates. To achieve this, it uses a surprisingly simple, yet incredibly powerful templating language which I recommend you to learn from [the official documentation][scriban-doc]. Once you 've learned it, let's see how Farkle integrates with it.

To integrate with Scriban, external libraries like Farkle expose certain variables and functions to Scriban templates. The variables exposed by Farkle are the following:

* `file_extension`: If you did not specify the file name of the generated file, it will have the same name as your grammar file, with the extension changed to the value of this variable. It is good practice to change it from the default `.out.txt`. For example, if Farkle gets ported to Pascal, you'd better write `{{ file_extension = ".pas" }}` at the beginning of the template for this language.

* `farkle`: An object containing information about Farkle itself.

  * `farkle.version`: The version of the tool used to generate the template.

* `grammar`: An object of type `Farkle.Grammar.Grammar` that represents the grammar we are generating a template for. Its members are the same, but be careful of Scriban's naming conventions. For example, the `NoiseSymbols` property becomes `noise_symbols`.

* `grammar_path`: The path to the grammar file that was given.

* `namespace`: The namespace of the generated source file. Defaults to the grammar's name. You can change it in the command line tools with the `--namespace` option. If your template does not generate source code, you can safely ignore this variable.

* `to_base_64 <bool>`: Returns the EGT file as a Base64-encoded string. If you pass `true`, it will add line breaks every 76 characters.

* `fmt <terminal or production> <case> <separator>`: Formats a symbol or a production into a string. You can specify the case of the generated string (`upper_case`, `lower_case`, `pascal_case`, `camel_case`), as well as the separator between the members of the production.

## Using the custom template

Having written your template, it's time to use it to generate some code.

### Installing Farkle.Tools

But before we continue, I haven't told you how to install Farkle.Tools. This hand-made CLI tool is available [on NuGet][farkle-tools-nuget] and is ready to be installed by all of you. You just need .NET Core version 3.1 or higher to be installed, and to run this command:

`dotnet tool install -g Farkle.Tools`

After you have installed the tools, you can tell them to use your template by running:

`farke new -t MyWonderfulTemplate.scriban MyMarvelousGrammar.egt`

Your file will be placed according to the previously mentioned rule. You can specify where to generate it by appending `-o MyFascinatingFile.txt` to the previous command. And of course you can see all the available commands by typing `farkle new --help`.

---

So, I hope you enjoyed this little reference. If you did, don't forget to give Farkle.Tools a try, and maybe you feel especially willing to create some templates today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[scriban]: https://github.com/lunet-io/scriban
[scriban-doc]: https://github.com/lunet-io/scriban/blob/master/doc/language.md
[gold-properties]: http://www.goldparser.org/doc/grammars/define-properties.htm
[farkle-tools-nuget]: https://nuget.org/packages/Farkle.Tools
