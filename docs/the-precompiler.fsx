(*** hide ***)
#I "../temp/referencedocs-publish/"
#r "Farkle.dll"

(**
# Farkle's precompiler

Every time an app using Farkle starts, it builds the parser tables for its grammars. This process takes some time and it will take even more if the app does not reuse the runtime Farkles it creates.

Most apps need to parse a static grammar whose specification never changes between program executions. For example, a compiler or a JSON parsing library will parse text from the same language every time you use them. Farkle would spend time building these parsing tables that do not depend on user input and will always be the same. It wouldn't hurt a program like a web app parsing lots of input strings, but for a compiler that parses only one file per invocation, building that grammar every time the program runs would impose an unnecessary overhead, maybe more than the time spent for the rest of the program if the grammar is big.

What is more, Farkle does not report any grammar error (such as an LALR conflict) until it's too late: text was attempted to be parsed with a faulty grammar. Wouldn't it be better if these errors were caught earlier in the app's development lifecycle?

One of Farkle's new features that came with version 6 is called _the precompiler_. The precompiler addresses this inherent limitation of Farkle's grammars being objects defined in code. Instead of building them every time, the grammar's parser tables are built _ahead of time_ and stored in the program's assembly when it gets compiled. When that program is executed, instead of building the parser tables, it loads the precompiled grammar from the assembly, which is orders of magnitude faster.

> [__Using the precompiler with Visual Studio for Windows requires extra steps.__](#Building-from-an-IDE)

## How to use it

Using the precompiler does not differ very much from regularly using Farkle.

### Preparing the your code

In F# designtime Farkles can be marked to be precompiled by applying the `RuntimeFarkle.markForPrecompile` function at the end. To build them, instead of using `RuntimeFarkle.build`, you have to use `RuntimeFarkle.buildPrecompiled` like in the example:
*)

open Farkle
open Farkle.Builder

let precompilableDesigntime =
    "My complicated language"
    ||= [!@ beginning .>>. middle .>>. ``end`` => (fun b m e -> b + m + e)]
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> RuntimeFarkle.markForPrecompile

let runtime = RuntimeFarkle.buildPrecompiled precompilableDesigntime

(**
Untyped designtime Farkles can be marked for precompilation with the `markForPrecompileU` function and can be built using the `RuntimeFarkle.buildPrecompiledUntyped` function.

---

In C# you have to call the `MarkForPrecompile` extension method and store its result in a field of type `PrecompilableDesigntimeFarkle` like the example:

``` csharp
using Farkle;
using Farkle.Builder;

public class MyLanguage {
    public static readonly PrecompilableDesigntimeFarkle<int> Designtime;
    public static readonly RuntimeFarkle<int> Runtime;

    static MyLanguage() {
        Designtime =
            Nonterminal.Create("My complicated language",
                beginning.Extended().Extend(middle).Extend(end).Finish((b, m, e) => b + m + e))
            .AddLineComment("//")
            .AddBlockComment("/*", "*/")
            .MarkForPrecompile();

        Runtime = Designtime.Build();
    }
}
```

As you see, the building methods in C# have the same name as before. The type for untyped precompilable designime Farkles is `PrecompilableDesigntimeFarkle`, without a type parameter.

### The rules

A precompilable designtime Farkle will be discovered and precompiled if it is declared in a `static readonly` _field_. In C# static properties with a getter are equivalent, as is in F# a let-bound value in a module, but it must not be mutable. Also, `static let`s in class declarations will not be recognized because they are not compiled as `readonly`.

This field can be of any visibility; public, internal, private, it doesn't matter. It will be detected even in nested classes or nested F# modules. It _cannot_ however be declared in a generic type.

In addition, the precompilable designtime Farkle must be marked in the assembly it is declared. Let's see a counterexample:

``` csharp
// Assembly A
public class AssemblyA {
    public static readonly PrecompilableDesigntimeFarkle<int> Designtime;

    static AssemblyA() {
        Designtime =
            // ...
            .MarkForPrecompile();
    }
}

// Assembly B
public class AssemblyB {
    public static readonly PrecompilableDesigntimeFarkle<int> WillNotBePrecompiled =
        AssemblyA.Designtime;

    public static readonly PrecompilableDesigntimeFarkle<int> WillBePrecompiled =
        AssemblyA.Designtime.InnerDesigntimeFarkle.MarkForPrecompile();
}
```

The precompiler will raise warnings to help you abide by the rules above.

All precompilable designtime Farkles within an assembly must have different names, or an error will be raised. You can use the `DesigntimeFarkle.rename` function or the `Rename` extension method to rename a designtime Farkle before marking it as precompilable.

Multiple fields referencing the same precompilable designtime Farkle do not pose a problem and will be precompiled only once.

### Preparing your project

With your designtime Farkles being ready to be precompiled, it's time to prepare your project file. Add a reference to [the `Farkle.Tools.MSBuild` package][msbuild] like that:

``` xml
<ItemGroup>
    <!-- The example's version numbers might be outdated. -->
    <PackageReference Include="Farkle" Version="6.3.0" />
    <PackageReference Include="Farkle.Tools.MSBuild" Version="6.3.0" PrivateAssets="all" />
</ItemGroup>
```

> __Important:__ The packages `Farkle` and `Farkle.Tools.MSBuild` must be at the same version. You will get a warning since Farkle 6.4.0 if they are not.

If you build your program now, you should get a message that your designtime Farkles' grammars got precompiled. Hooray! Your app's startup time will be now much faster.

__If you have marked your designtime Farkles as precompiled, using the precompiler is mandatory.__ Parsing will always eventually fail if you build a precompilable designtime Farkle without having used the precompiler.

## Conflict reports

Since Farkle 6.3.0, if a grammar you were going to precompile has LALR conflicts, Farkle will not display each of them as build errors, but will generate an HTML report containing all the parser's states, including their conflicting actions. You can take a look at [a sample conflict report](sample-conflict-report.html) for [the quick start guide's](quickstart.html) mathematical expression grammar, if we had not added operator precedence and associativity.

This feature is enabled by default and is expected to make diagnosing grammars with conflicts much easier. However if you want for some reason to disable conflict reports, you can see how to do it on the next section.

The conflict reports are stored in a temporary location and will be cleaned by MSBuild rebuilding or cleaning the project. Their paths are written to the `@(FarkleGeneratedConflictReports)` item, which can be used by a custom MSBuild target to do things like copy them elsewhere or upload them as CI artifacts. Such targets need to run after `FarkleRunPrecompiler`.

## Customizing the precompiler

The precompiler's behavior can be customized by the following MSBuild properties you can set in your project file:

``` xml
<PropertyGroup>
    <!-- Set it to false to disable the precompiler. As stated above however,
    disabling it will cause parsing these precompiled grammars to fail. -->
    <FarkleEnablePrecompiler>false</FarkleEnablePrecompiler>
    <!-- If set to true, Farkle will generate an HTML page
    describing each precompiled grammar. Defaults to false. -->
    <FarkleGenerateHtml>true</FarkleGenerateHtml>
    <!-- If set to false, Farkle will not generate an HTML report if any
    grammar has conflicts but display each of them as build errors.
    Obsolete since Farkle 6.4.0. -->
    <FarkleGenerateConflictReports>false</FarkleGenerateConflictReports>
    <!-- New in Farkle 6.4.0, can allow both detailed build errors and
    conflict reports to be emitted when the precompiler encounters conflicts.
    Allowed values are: Both, ErrorsOnly, ReportOnly. If present,
    FarkleGenerateConflictReports will be ignored. -->
    <FarklePrecompilerErrorMode>Both</FarklePrecompilerErrorMode>
</PropertyGroup>
```

The `FarkleGenerateHtml` property uses Farkle's templating engine which is described [in its own page](templates.html#Creating-HTML-pages).

Furthermore, Farkle's precompiler is based on [Sigourney], which can be globally disabled by setting the `SigourneyEnable` property to false.

## Some final notes

### Composability

The name "precompilable designtime Farkle" is a bit misleading, because these objects do not implement the `DesigntimeFarkle` interface. This means that you cannot compose a precompilable designtime Farkle to form a bigger grammar, as you can do with an actual designtime Farkle. This incompatibility ensures that you are using the `markForPrecompile` family of functions correctly, by applying them once at the end.

To get the actual designtime Farkle behind a precompilable one, you have to use the `InnerDesigntimeFarkle` property:
*)

let composable = precompilableDesigntime.InnerDesigntimeFarkle

(**
### Beware of code execution

Farkle's precompiler executes part of your project's code; the necessary static constructors to create your precompilable designtime Farkles. This code can do literally anything, but it is your responsibility to keep it short and without adverse side-effects. Similarly, it is your responsibility to not build untrusted projects that use the precompiler. Consuming 3rd-party libraries with precompiled grammars however will not execute arbitrary code.

### Beware of non-determinism

Farkle's precompiler was made for grammars that are fixed, which is the reason it only works on static readonly fields: once you created it in your code, you cannot change it. Otherwise, what good would the precompiler be?

You can always call a non-deterministic function like `DateTime.Now` that will make your designtime Farkle parse integers in the hexadecimal format in your birthday, and in the decimal format in all other days. If you build your app on your birthday, it will produce bizarre results on all the other days, and if you build it on a day other than your birthday, it will work every time, except on your birthday (the worst birthday present). __Just don't do it.__ Farkle cannot be made to detect such things, and you are not getting any smarter by doing it.

### Using the precompiler in AOT environments

The `markForPrecompile` family of functions use the `Assembly.GetCallingAssembly()` method to determine the assembly the precompiled grammar is in, but this function is not supported in AOT environments.

Farkle 6.5.0 introduced overloads to the `MarkForPrecompile` extension method that allow manually specifying the assembly. Here's how to use them in both languages:
*)

type Dummy = class end

let designtime = literal "foo"

// We get the assembly of a dummy type we defined. We could have also called Assembly.GetExecutingAssembly().
// The extension methods are the same in both C# and F#; there are no equivalent curried functions.
let worksInAot = designtime.MarkForPrecompile(typeof<Dummy>.Assembly)

(**

``` csharp
using Farkle;
using Farkle.Builder;

public class MyLanguage {
    public static readonly PrecompilableDesigntimeFarkle<int> Designtime;
    public static readonly RuntimeFarkle<int> Runtime;

    static MyLanguage() {
        Designtime =
            Nonterminal.Create("My complicated language",
                beginning.Extended().Extend(middle).Extend(end).Finish((b, m, e) => b + m + e))
            .AddLineComment("//")
            .AddBlockComment("/*", "*/")
            .MarkForPrecompile(typeof(MyLanguage).Assembly);

        Runtime = Designtime.Build();
    }
}
```

### Building from an IDE

And last but not least, until version 6.3.0, the precompiler would not work when building an app on a .NET Framework edition of MSBuild, such as Visual Studio for Windows or the `msbuild` command.

.NET Core and .NET (these terms are used interchangeably) have APIs that allow the precompiler to dynamically load an assembly, get its precompilable grammars, and unload it. To do the same thing on .NET Framework was extremely hard, which is why Farkle runs the precompiler on an external process, known as _precompiler worker_. To setup the precompiler worker, install the .NET tool `Farkle.Tools` with the following command:

```
dotnet tool install Farkle.Tools
```

The version of `Farkle.Tools` must be the same with the packages `Farkle` and `Farkle.Tools.MSBuild` you use in your project. Otherwise errors are very likely to occur. The tool must also be installed _locally_, otherwise Farkle will not see it.

Due to limitations arising from the precompiler worker's out-of-process execution model, it can only precompile assemblies compatible with `Farkle.Tools.MSBuild`'s supported framework. This framework is generally the latest LTS version of .NET (.NET 6 since Farkle 6.4.0, .NET Core 3.1 prior to that). For example, when .NET 7 gets released, the precompiler worker will not be able to precompile assemblies targeting it, until the release of .NET 8. In addition, the corresponding .NET SDK version must be installed, even if a newer version of the SDK is used for the rest of the project.

The recommended way to build an app that uses the precompiler is through .NET SDK commands like `dotnet build`, `dotnet run` and `dotnet msbuild`. It is faster, more stable and more supported. Visual Studio for Windows cannot run MSBuild on .NET. [A suggestion on Visual Studio Developer Community][vs-suggestion] has been filed but Microsoft responded that it won't be implemented anytime soon. Readers of this guide that are affected are encouraged to upvote the above suggestion. When not using Visual Studio for Windows, Farkle will raise a warning suggesting you to move to the .NET SDK.

Rider however can use the .NET edition of MSBuild with a simple workaround. Open its settings, go to "Build, Execution, Deployment", "Toolset and Build", "Use MSBuild version", and select an MSBuild executable from the .NET SDK (it typically has a `.dll` extension).

![The Settings window in JetBrains Rider](img/rider_msbuild_workaround.png)

The fact that the precompiler runs on .NET Core doesn't mean that it won't work on projects targeting the .NET Framework. Precompiling a .NET Framework assembly will still load it to the .NET Core-based precompiler. While it sometimes works due to a compatibility shim, don't hold your breath that it will always work and you'd better not precompile designtime Farkles in assemblies that use .NET Framework-only features like ASP.NET Web Forms. In such scenarios you are recommended to move your Farkle-specific code to a separate library targeting .NET Standard.

---

So I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try, and maybe you feel especially precompiled today, and want to hit the star button as well. I hope that all of you have an wonderful day, and to see you soon. Goodbye!

[msbuild]: https://www.nuget.org/packages/Farkle.Tools.MSBuild
[Sigourney]: https://github.com/teo-tsirpanis/Sigourney
[vs-suggestion]: https://developercommunity2.visualstudio.com/t/Allow-building-SDK-style-projects-with-t/1331985
*)
