# Farkle's precompiler

Every time an app using Farkle starts, it generates the parser tables for its grammars. This process takes some time, and it take even more, if the app does not reuse the runtime Farkles it creates.

Most apps need to parse a static grammar whose specification never changes between program executions. For example, a compiler or a JSON parsing library will parse text from the same language every time you use them. Farkle would spend time generating these parsing tables that do not depend on user input and will always be the same. It wouldn't hurt a program like a REST API server parsing lots of input strings, but for a compiler that parses only one file, building that grammar every time it is executed would impose an unnecssary overhead, maybe more than the time spent for the rest of the program, if the grammar is big.

What is more, Farkle does not report any grammar error (such as an LALR conflict) until it's too late: text was attempted to be parsed with a faulty grammar. Wouldn't it be better if these errors were caught earlier in the app's developemnt lifecycle?

One of Farkle's new features that came with version 6 is called _the precompiler_. The precompiler addresses this inherent limitation of Farkle's grammars being objects defined in code. Instead of building it every time, the grammar's parser tables are built __ahead of time__ and stored in the program's assembly when it gets compiled. When that program is executed, instead of building the parser tables, it loads the precompiled grammar from the assembly, which is orders of magnitude faster.

Moreover, Farkle will dynamically generate code to even further optimize the parsing process. To avoid the code generation overhead, dynamic code generation is employed only on precompilable grammars, when the .NET Standard 2.1 edition of Farkle (or any more modern framework) is used, and when [the underlying runtime compiles dynamic code][is-dynamic-code-compiled].

## How to use it

Using the precompiler is surprisingly simple and does not differ very much from regularly using Farkle.

### Preparing the your code

Let's say you have a very complicated designtime Farkle that you want its grammar to be precompiled. The first thing to do is to use the `RuntimeFarkle.markForPrecompile` function. In F# it can be done by applying a the `RuntimeFarkle.markForPrecompile` function at the end of your designtime Farkle:

``` fsharp
open Farkle;
open Farkle.Builder;
let designtime =
    "My complicated language"
    ||= [!@ beginning .>>. middle .>>. ``end`` => (fun b m e -> b + m + e)]
    |> DesigntimeFarkle.addLineComment "//"
    |> DesigntimeFarkle.addBlockComment "/*" "*/"
    |> RuntimeFarkle.markForPrecompile

let runtime = RuntimeFarkle.build designtime
```

Untyped designtime Farkles can be marked for precompilation with the `markForPrecompileU` function.

---

In C#, you have to do two things. First, you have to declare your designtime Farkle as a `PrecompilableDesigntimeFarkle`. Second, you have to put a `.MarkForPrecompile()` at the end, like what to do in F#.

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

The type for untyped precompilable designime Farkles is `PrecompilableDesigntimeFarkle`, without a type parameter.

### The rules

A precompilable designtime Farkle will be discovered by Farkle if it follows these rules:

With this simple function (or extension method), Farkle will be able to discover this designtime Farkle in your assembly and precompile it. It will be able to actually find it if you follow these rules:

* The designtime Farkle must be declared in a `static readonly` _field_ (not property). For F#, a let-bound value in a module is equivalent, but it must not be mutable.

* The designtime Farkle's field can be of any visibility (public, internal, private, it doesn't matter). It will be detected even in nested classes or modules.

* The `markForPrecompile` function must be the absolute last function to be applied in the designtime Farkle.

* The `markForPrecompile` function must be called from the assembly the designtime Farkle will be stored.

* The field of the precompilable designtime Farkle must be either a typed or untyped `PrecompilableDesigntimeFarkle`:

``` csharp
public class MyLanguage {
    // This will be precompiled.
    public static readonly PrecompilableDesigntimeFarkle Foo = MyUntypedDesigntimeFarkle.MarkForPrecompile();
    // But not this.
    public static readonly DesigntimeFarkle Bar = MyUntypedDesigntimeFarkle.MarkForPrecompile();
}
```

* All precompilable designtime Farkles within an assembly must have different names, or an error will be raised during precompiling. Use the `DesigntimeFarkle.rename` function or the `Rename` extension method to rename a designtime Farkle.

* Multiple references to the same precompilable designtime Farkle do not pose a problem and will be precompiled once.

If any of the rules above is violated, building your app will not fail (unless a name collision was detected, which will always fail the build) and the designtime Farkle will not be precompiled but will be otherwise perfectly functional.

> __Note:__ It makes sense to mark a designtime Farkle as precompilable and immediately build it without actually precompiling it. Merely marking it as precompilable will allow the dynamic code generation optimizations.

### Preparing your project

With your designtime Farkles being ready to be precompiled, it's time to prepare your project file. Add a reference to [the `Farkle.Tools.MSBuild` package][msbuild] like that:

``` xml
<ItemGroup>
    <PackageReference Include="Farkle" Version="6.*" />
    <PackageReference Include="Farkle.Tools.MSBuild" Version="6.*" PrivateAssets="all" />
</ItemGroup>
```

> __Important:__ The packages `Farkle` and `Farkle.Tools.MSBuild` must be at the same version.

If you build your program now, you should get a message that your designtime Farkles' grammars got precompiled. Hooray! With your designtime Farkles being precompiled, building them is now much, much faster.

## Customizing the precompiler

The precompiler can be customized by the following optional MSBuild properties you can set in your project file:

``` xml
<PropertyGroup>
    <!-- Set it to false to disable the precompiler. Your grammar will still
    work, but without the initial performance boost the precompiler offers. -->
    <FarkleEnablePrecompiler>false</FarkleEnablePrecompiler>
    <!-- If it is set to true, grammar errors will raise a warning
    instead of an error and not fail the entire build. -->
    <FarkleSuppressGrammarErrors>true</FarkleSuppressGrammarErrors>
</PropertyGroup>
```

Furthermore, Farkle's precompiler is based on [Sigourney], which means that it can be disabled by setting the `SigourneyEnable` property to false.

## Some final notes

### Beware of non-determinism

Farkle's precompiler was made for grammars that are static, that's the reason it only works on static readonly fields: once you created it in your code, you cannot change it. Otherwise, what good would the precompiler be?

You can always call a non-deterministic function like `DateTime.Now()` that will make your designtime Farkle parse integers in the hexadecimal format in your birthday, and in the decimal format in all other days. If you build your app on your birthday, it will produce bizarre results on all the other days, and if you build it on a day other than your birthday, it will work every time, except of your birthday (the worst birthday present). __Just don't do it.__ Farkle cannot be made to detect such things, and you are not getting any smarter by doing it.

### Building from an IDE

And last but not least, the precompiler will not work when running a .NET Framework-based edition of MSBuild. This includes building from IDEs such as Visual Studio. The recommended way to build an app that uses the precompiler is through `dotnet build` and its friends. This doesn't mean that the precompiler won't work on .NET Framework assemblies; you have to use the new project format and build with the .NET Core SDK; it will normally work.

> __Note__: Precompiling a .NET Framework assembly will load it to the .NET Core-based precompiler. While it sometimes works due to a compatibility shim, don't hold your breath that it will always work and you'd better not precompile designtime Farkles in assemblies that use .NET Framework-only features. It might work, it might fail, who knows? And why are you still using the .NET Framework?

Rider however can use the precompiler with a simple workaround. Open its settings, go to "Build, Execution, Deployment", "Toolset and Build", "Use MSBuild version", and select an MSBuild executable from the .NET Core SDK (it typically has a `.dll` extension).

![The Settings window in JetBrains Rider](img/rider_msbuild_workaround.png)

---

So I hope you enjoyed this little tutorial. If you did, don't forget to give Farkle a try, and maybe you feel especially precompiled today, and want to hit the star button as well. I hope that all of you have an wonderful day, and to see you soon. Goodbye!

[is-dynamic-code-compiled]: https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature.isdynamiccodecompiled
[msbuild]: https://www.nuget.org/packages/Farkle.Tools.MSBuild
[Sigourney]: https://github.com/teo-tsirpanis/Sigourney
