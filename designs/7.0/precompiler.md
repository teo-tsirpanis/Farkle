# Farkle 7's precompiler

Since its introduction in version 6.0, the precompiler has been Farkle's killer feature. It bridges the advantages of parser generators and parser combinator libraries, and gives users both good performance and good developer experience. The precompiler has worked by running an MSBuild task after compilation, which executes the compiled assembly in order to discover grammars to precompile, builds the grammars, and injects the binary grammar data back into the assembly.

For Farkle 7, the precompiler will still follow this approach in general. Using source generation was considered, but while it might have enhanced developer experience in some regard, Farkle's API is not compatible with source generation (and the API being this way is the reason why Farkle exists in the first place), and it would have made the precompiler specific to C# (F# compatibility is important). However, beyond that, the precompiler will be completely redesigned to address the biggest shortcomings of Farkle 6's implementation.

## Requirements

Requirements with a 🆕 are new for Farkle 7.

* __Eliminate the need to build statically known grammars at runtime.__
  * Reduce startup time.
  * Allow most grammar builder code to be trimmed away.
* __Emit compile-time diagnostics for problems with the grammar.__
* __Support Native AOT.__ This was added later, and requires an explicit gesture to pass the grammar's assembly to Farkle, instead of having it call `Assembly.GetCallingAssembly()`.
* __Support running the precompiler in both .NET Framework and .NET flavors of MSBuild.__ This is required to run the precompiler in builds launched by Visual Studio, which uses .NET Framework MSBuild. Because the precompiler uses APIs exclusive to .NET, this is not trivial, and requires using an out-of-process "precompiler worker" that runs on .NET. Writing the precompiler worker was very difficult, and it has been fragile under certain circumstances. Fortunately, MSBuild in Visual Studio 2026 introduced first-class support for launching .NET tasks out-of-process, which will make Farkle's custom implementation unnecessary.
* 🆕 __Eliminate the use of reflection when loading a precompiled grammar.__
* 🆕 __Allow unused precompiled grammars to be trimmed away.__
* 🆕 __Build the grammar with the application's copy of the Farkle library.__
  * Support a degree of version flexibility between the precompiler and the Farkle library.
  * Support using the precompiler with development builds of the Farkle library.
  * Support using the precompiler on assemblies that embed the Farkle library's source code.

## The API

### Activating the precompiler

In Farkle 6, the precompiler was being activated by searching `static readonly` fields of type `PrecompilableDesigntimeFarkle<T>` or `PrecompilableDesigntimeFarkle`, and getting their value. This is not ideal, because getting the value of a static field causes its class' static constructor to run, which might run unrelated code and does not work well if an exception is thrown, and because activating on all fields of a certain type is too "magic" and implicit, and does not match best practices for source code generators.

In Farkle 7, the precompiler will be activated by placing the following attributes on factory methods:

```csharp
namespace Farkle.Builder;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PrecompilerInputAttribute : Attribute
{
    public PrecompilerInputAttribute();

    public string? Key { get; set; }

    // Mirroring all properties of BuilderOptions.

    public int MaxTokenizerStates { get; set; } = -1;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PrecompilerOutputAttribute : Attribute
{
    public PrecompilerOutputAttribute();

    public string? Key { get; set; }

    public bool SyntaxCheck { get; set; }
}
```

Each precompiled grammar will be defined by a static method (the _input method_) marked with `PrecompilerInputAttribute`, that accepts no parameters, and returns `IGrammarBuilder<T>` or `IGrammarBuilder`. The precompiler will call the input method to get the grammar to build.

Each input method can be associated with zero or more static methods (the _output methods_) marked with `PrecompilerOutputAttribute`, that accept no parameters, and return one of the possible output types. The precompiler will replace the output methods' body, to load the precompiled grammar from the assembly. The possible output types are:

* `Grammar`
* `CharParser<T>`, if the input method returns `IGrammarBuilder<TInput>`. `TInput` must be assignable to `T`.
* `CharParser<T?> where T : class`, if the input method returns `IGrammarBuilder`, or the attribute's `SyntaxCheck` property is set to true. `T` must be a reference type.

The input and output methods of a precompiled grammar must be declared in the same type.[^same-type] A type can contain more than one precompiled grammar, and the attribute property `Key` will be used to disambiguate each grammar's factory methods.

### Helper APIs

Precompiler output methods will have their body replaced by the precompiler, but they still need an implementation when writing them in source code.[^extern-t] Possible options include throwing an exception, returning `null`, or calling the input method and building the grammar (the latter can be used to seamlessly allow turning the precompiler off). Following the principle of deferring errors in the parser API, we will add a helper function that returns a dummy `CharParser<T>`, that will always fail with a message indicating that the grammar has not been precompiled.

```csharp
namespace Farkle;

public static partial class CharParser
{
    public static CharParser<T> MustPrecompile<T>();
}
```

Calling `CharParser.MustPrecompile<T>()` is preferred over throwing an exception when writing precompiler output methods. There won't be an equivalent for `Grammar`, because only the parser API supports deferred error reporting.

### Before and after

This is how to use the precompiler in Farkle 6:

```csharp
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

And this is how to use the precompiler in Farkle 7:

```csharp
using Farkle;
using Farkle.Builder;

public class MyLanguage {
    [PrecompilerInput]
    public static IGrammarBuilder<int> GetMyLanguage() =>
        Nonterminal.Create("My complicated language",
            beginning.Extended().Extend(middle).Extend(end).Finish((b, m, e) => b + m + e))
        .AddLineComment("//")
        .AddBlockComment("/*", "*/");

    [PrecompilerOutput]
    public static CharParser<int> GetMyLanguageParser() => CharParser.MustPrecompile<int>();
}
```

## Implementation

### Code generation

Farkle 6's precompiler embeds each precompiled grammar as a manifest resource in the assembly, and uses reflection to load it at runtime. Given that we don't want to use reflection in Farkle 7, we have to use a different approach.

In Farkle 7 we will embed the grammar in an RVA field, and patch the output method to call a special API to load the grammar from it. At the cost of slightly more complex IL weaving, this will satisfy both the requirement to not use reflection, and to allow trimming unused grammars away.

The API to load a grammar from an RVA field will look like this:

The following APIs will be defined for the precompiler-generated code to call:

```csharp
using Farkle.Builder;

namespace Farkle.Runtime;

public static class PrecompilerEntryPoints
{
    public static unsafe Grammar LoadGrammar(byte* data, int length, RuntimeTypeHandle containingType);

    public static unsafe CharParser<T> LoadCharParser(byte* data, int length, RuntimeTypeHandle containingType, Func<IGrammarBuilder<T>> builderFactory);

    public static unsafe CharParser<T?> LoadCharParserSyntaxChecker<T>(byte* data, int length, RuntimeTypeHandle containingType, Func<IGrammarBuilder>? builderFactory) where T : class?;
}
```

The returned grammar object will directly reference the RVA field's bytes. It will hold a reference to the containing type, in order to prevent it from being unloaded. Also, because a loaded assembly is considered trusted, loading grammars from this API will skip content validation, making the complexity of loading the grammar $O(1)$ in both time and memory overhead.

The APIs that load `CharParser<T>` instances will accept a delegate to the input method, in order to build the semantic provider, and to rebuild the grammar on Hot Reload scenarios. Because syntax checkers do not have a semantic provider, the input method delegate is optional, and can be `null` if Hot Reload is not supported (allowing the input method to be trimmed).

### Assembly hosting

> _May I have your attention please? Will the real Farkle please stand up? We're gonna have a problem here…_

The third new requirement is the hardest, and is the reason for the precompiler's complete redesign. For some background, when the precompiler runs, there are two Farkle assemblies involved; the assembly referenced by the user (the _user assembly_), and the assembly contained in the `Farkle.Tools.MSBuild` package (the _host assembly_). This poses the question of which assembly to use to build the grammar.

In Farkle 6, the host assembly was used. The advantage of this is that the precompiler could seamlessly operate on the assembly being precompiled, just by hooking `AssemblyLoadContext.Load`, and using ordinary Farkle APIs to build the grammar. However, this approach is fragile, and requires the versions of the `Farkle` and `Farkle.Tools.MSBuild` packages to exactly match (leading to silent surprises otherwise), and also inhibits some scenarios, like using local or source-embedded builds of the Farkle library, or precompiling the string regex grammar in the Farkle library itself.

In Farkle 7, we will use the user assembly to build the grammar. It will increase flexibility and enable the aforementioned scenarios, at the cost of having a well-defined interface between the precompiler and the user assembly. This interface will be built on top of [COM#](comsharp.md), and will not be part of Farkle's public API; breaking it will be allowed at any time, with the consequence of requiring users to have a matching version of `Farkle.Tools.MSBuild`.

The precompiler consists of three main steps: _discovering_ grammars to precompile in the assembly, _building_ these grammars, and _weaving_ the assembly to embed the precompiled grammars. The user assembly will perform the first two steps, and weaving will be the responsibility of the precompiler side. In general, the more we can push to the user assembly, the better, but weaving would require a complex precompiler interface, and is not going to frequently change to matter much.

## Metadata

> [!NOTE]
> This section describes the only pieces of metadata that can be relied upon by third party code. Any other metadata the precompiler might emit is considered an implementation detail, and may change at any time between versions.

In order to support statically retrieving precompiled grammars from an assembly, the precompiler will add the following attribute to the RVA field that contains the grammar data:

```csharp
namespace Farkle.Runtime;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class PrecompiledGrammarAttribute : Attribute
{
    public string? Key { get; set; }
}
```

The following conditions must hold for each field that has this attribute:

* The field must have the `FieldAttributes.HasFieldRVA` flag set.
* The field's signature must refer to a value type defined with an index to a `TypeDef` metadata table. In other words, the field's type must be a value type declared in the same assembly.
* The field's type must have an entry in the `ClassLayout` metadata table, with a non-zero `ClassSize` column.

The field will be defined in the same type where the precompiled grammar was defined. The attribute's `Key` property will correspond to the `Key` property of the input and output attributes.

The attribute's type must be either defined in the same assembly as the field, or referenced from the `Farkle` assembly. An assembly must not contain attribute instances from more than one source.

[^same-type]: Same type in the ECMA-335 metadata sense. It would be lovely if we could support multiple precompiled grammars in separate C# local functions, but that's not possible, because local functions are not an ECMA concept.

[^extern-t]: Declaring the method as `extern` will not work, because it causes type load errors, even if the method is never called.
