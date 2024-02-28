---
category: Advanced
categoryindex: 2
index: 2
description: A guide on how to create and use templates with Farkle.
---
# Templating Reference

Farkle comes with a templating system which further helps developers work with the grammars they create. This system is a more powerful edition of both GOLD Parser's ["Create Webpage"][gold-webpage] and ["Create Skeleton Program"][gold-skeleton] tools. In this guide, we will first see how to create an HTML page describing our grammar, and next we will see how to create our own templates. So, are you ready? Let's do this!

## Preparing ourselves

Before we start, we first have to install [Farkle's CLI tool from NuGet][farkle-tools-nuget] by opening a command prompt and running

```
dotnet tool install -g Farkle.Tools
```

### Supported input types

The CLI tool can work with grammars from three kinds of files:

* GOLD Parser 5.0 Enhanced Grammar Tables files with the `.egt` extension.
* .NET assemblies with [precompiled](the-precompiler.html) grammars. These assemblies can target any framework and their dependencies don't have to be present. Because precompiled grammars are stored in the assembly's embedded resources, no code from them is executed.
* .NET projects with precompiled grammars. The projects must have already been built; Farkle does not do it by itself at the moment.

If we don't explicitly specify an input, the CLI tool will try to find a project in the current directory. If only one is found, it will be used. Otherwise the tool will fail with an error.

### Dealing with multiple precompiled grammars

If the assembly or project passed to the CLI tool has only one precompiled grammar, it will be automatically used. If it has more than one however, we can tell it which one to use by adding a `::MyGrammar` to the end of the input path.

To see an example, if you want to pick a specific grammar from an assembly or project, you can do it by writing `MyAssembly.dll::MyGrammar` or `MyProject.csproj::MyGrammar` respectively. If the current directory has only one project and you are bored to type it, you can write `::MyGrammar`.

You can see the names of all precompiled grammars with the `farkle list` command which also takes an assembly, a project, or tries to find one on its own.

## Creating HTML pages

For the rest of this guide, we will assume that we have a project with a precompiled grammar next to us. Once we build it, we can create an HTML page by running `farkle new`.

If everything went well, we will see a file named like `MyAwesomeGrammar.html`. It describes the grammar's syntax, its LALR states, its DFA states and more. If you are just watching, take a look at a sample [HTML file generated for a JSON grammar](JSON-generated.html) to get a better idea.

These HTML files can be customized by omitting the state tables, the CSS styling, or by adding custom content at the end if their `<head>`. Run `farkle new --help` to get all the available options.

You can automatically generate an HTML file from your project's precompiled grammars by adding the following lines in it:

``` xml
<PropertyGroup>
  <FarkleGenerateHtml>true</FarkleGenerateHtml>
</PropertyGroup>
```

The HTML files will be generated to your project's build output directory and cannot be customized in any way. They are intended for development use; for most customizability it is recommended to use the CLI tool.

## Creating your own templates

Instead of GOLD's custom and limited templating language, Farkle's templates use [Scriban], which features a much more powerful templating language. If you want to create your own templates, I recommend to first learn it from [Scriban's official documentation][scriban-doc].

### Variables

Farkle's templates can use the following Scriban variables:

* `file_extension`: This variable is used to change the default extension of the output file. For example if you are creating an HTML template you would add a `{{ file_extension = ".html" }} at the beginning.

* `farkle.version`: The version of the CLI tool.

* `grammar`: A Farkle `Grammar` object that represents the input grammar.

  * `grammar.productions_groupped`: A `System.Linq.IGrouping<Nonterminal,Production>` object that groups productions by their head nonterminal.

* `grammar_path`: The path to the given input file; either a grammar file or an assembly. When processing project files this variable will have the project's underlying assembly.

* `properties`: An object that holds custom properties passed by the `-prop` CLI argument. For example, if you pass `-prop foo bar` to the CLI tool and write `{{ properties.foo }}` in your template, it will be evaluated as `bar`.

Scriban imports all properties of Farkle's objects but changes their names. Take a look at [Farkle's built-in templates][builtin-templates] to get an idea how to write your own template, but keep in mind that the HTML templates use some constructs not available to custom templates, like Scriban's `import` statement and some other internal functions.

### Functions

The templates can furthermore use the following functions:

* `to_base_64 <bool>`: Returns the grammar file as a Base64-encoded string. If you pass `true`, it will add line breaks every 76 characters.

> __Note:__ Precompiled grammars from an assembly or project are encoded in a format called EGTneo which is incompatible with GOLD Parser's EGT's format. When creating a template from an EGT file however, the `to_base_64` function will return the Base-64 representation of that file.

* `group_dfa_edge <dfa_state>`: Returns an `IGrouping` object that groups the edges of a DFA state by their action.

* `fmt <terminal or production> <case> <separator>`: Formats a terminal or a production to a string. You can specify the case of the generated string (`upper_case`, `lower_case`, `pascal_case`, `camel_case`), as well as the separator between the members of the production.

### Using custom templates

A custom template can be rendered by writing `farkle new -t MyCustomTemplate.scriban`. Of `farkle new`'s arguments, custom templates only support `-prop`, as explained above.

---

So, I hope you enjoyed this little guide. If you did, don't forget to give Farkle.Tools a try, and maybe you feel especially willing to create some templates today, and want to hit the star button as well. I hope that all of you have a wonderful day, and to see you soon. Goodbye!

[gold-webpage]: http://www.goldparser.org/doc/builder-cmd/goldwebpage.htm
[gold-skeleton]: http://www.goldparser.org/doc/builder-cmd/goldprog.htm
[scriban]: https://github.com/scriban/scriban
[scriban-doc]: https://github.com/scriban/scriban/blob/master/doc/language.md
[builtin-templates]: https://github.com/teo-tsirpanis/Farkle/tree/mainstream/src/Farkle.Tools.Shared/builtin-templates
[farkle-tools-nuget]: https://nuget.org/packages/Farkle.Tools
