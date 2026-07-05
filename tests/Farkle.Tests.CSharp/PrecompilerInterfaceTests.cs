// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Reflection;
using ComSharp;
using Farkle.Builder;
using Farkle.Grammars;
using Farkle.Diagnostics.Builder;
using Farkle.Runtime;
using DiagnosticSeverity = ComSharp.DiagnosticSeverity;

namespace Farkle.Tests.CSharp;

internal class PrecompilerInterfaceTests
{
    private IPrecompilerInterface _precompilerInterface;

    [SetUp]
    public void Setup()
    {
        var intf = PrecompilerEntryPoints.GetPrecompilerInterface();
        _precompilerInterface = (IPrecompilerInterface)PrecompilerInterfaceWrappers.Instance.ConvertToDotNet(intf)!;
        Assert.That(_precompilerInterface, Is.Not.Null);
    }

    [TestCaseSource(nameof(DiscovererTestCases))]
    public void TestDiscoverer(Type type, string[] expectedDiagnostics, int expectedGrammars, int expectedOutputMethods)
    {
        List<string?> diagnostics = [];
        BuilderLogger log = new() { LogLevel = Diagnostics.DiagnosticSeverity.Warning };
        log.OnDiagnostic += x =>
        {
            diagnostics.Add(x.Code);
        };

        var options = new PrecompilerOptions(log);
        var grammars = _precompilerInterface.DiscoverAndPrecompile([type], options).ToList();
        var inputMethods = grammars.Select(x => type.Module.ResolveMethod(x.InputMethodMetadataToken)).ToList();
        var outputMethods = grammars.SelectMany(x => x.OutputMethods).Select(x => type.Module.ResolveMethod(x.MetadataToken)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics, Is.EquivalentTo(expectedDiagnostics));
            Assert.That(grammars, Has.Count.EqualTo(expectedGrammars));
            Assert.That(inputMethods, Has.All.Attribute<PrecompilerInputAttribute>());
            Assert.That(outputMethods, Has.Count.EqualTo(expectedOutputMethods));
            Assert.That(outputMethods, Has.All.Attribute<PrecompilerOutputAttribute>());
        }
    }

    [DiscovererTestCase]
    private static class Success
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder1() => DummyBuilder;

        [PrecompilerOutput]
        public static Grammar Grammar1() => null!;

        [PrecompilerOutput]
        public static CharParser<string?> Parser1() => null!;

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder<int> Builder2() => DummyBuilderTyped<int>();

        [PrecompilerOutput(Key = "1")]
        public static CharParser<int> Parser2() => null!;

        [PrecompilerInput(Key = "2")]
        public static IGrammarBuilder<string?> Builder3() => DummyBuilderTyped<string>();

        [PrecompilerOutput(Key = "2")]
        public static CharParser<string> Parser3() => null!;

        [PrecompilerOutput(Key = "2")]
        public static CharParser<object> Parser3_2() => null!;

        [PrecompilerOutput(Key = "2", SyntaxCheck = true)]
        public static CharParser<AssemblyName?> SyntaxChecker3() => null!;

        [PrecompilerInput(Key = "3")]
        public static Nonterminal<int> Builder4() => DummyNonterminalTyped<int>();

        [PrecompilerOutput(Key = "3")]
        public static CharParser<int> Parser4() => null!;

        [PrecompilerInput(Key = "4")]
        public static Builder.Untyped.Nonterminal Builder5() => DummyNonterminal;

        [PrecompilerOutput(Key = "4")]
        public static CharParser<string?> Parser5() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0010", "FARKLE0010", "FARKLE0010", "FARKLE0010"], ExpectedGrammars = 1)]
    private sealed class Failure_InvalidInputMethod
    {
        [PrecompilerInput]
#pragma warning disable CA1822 // Mark members as static
        public IGrammarBuilder InstanceMethod() => DummyBuilder;
#pragma warning restore CA1822 // Mark members as static

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder Generic<T>() => Terminal.Literal(typeof(T).Name);

        [PrecompilerInput(Key = "2")]
        public static IGrammarBuilder HasParameters(int x) => Terminal.Literal(x.ToString());

        [PrecompilerInput(Key = "3")]
        public static int InvalidReturnType() => 0;

        [PrecompilerInput(Key = "4")]
        public static IGrammarBuilder Valid() => DummyBuilder;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0011", "FARKLE0011", "FARKLE0011", "FARKLE0011"], ExpectedOutputMethods = 1)]
    private sealed class Failure_InvalidOutputMethod
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder() => DummyBuilder;

        [PrecompilerOutput]
#pragma warning disable CA1822 // Mark members as static
        public CharParser<object> InstanceMethod() => null!;
#pragma warning restore CA1822 // Mark members as static

        [PrecompilerOutput]
        public static CharParser<object> Generic<T>() => null!;

        [PrecompilerOutput]
        public static CharParser<object> HasParameters(int x) => null!;

        [PrecompilerOutput]
        public static int InvalidReturnType() => 0;

        [PrecompilerOutput]
        public static CharParser<string?> Valid() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0010", "FARKLE0011", "FARKLE0011"], ExpectedGrammars = 0, ExpectedOutputMethods = 0)]
    private static class Failure_GenericClass<T>
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder() => DummyBuilder;

        [PrecompilerOutput]
        public static Grammar Grammar() => null!;

        [PrecompilerOutput]
        public static CharParser<string?> Parser() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0012"], ExpectedOutputMethods = 1)]
    private static class Failure_IncompatibleParserReturnType
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder() => DummyBuilder;

        [PrecompilerOutput]
        public static CharParser<int> Parser() => null!;

        [PrecompilerOutput]
        public static CharParser<string?> Valid() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0012", "FARKLE0012"], ExpectedOutputMethods = 4)]
    private static class Failure_IncompatibleParserReturnType_Typed
    {
        [PrecompilerInput]
        public static IGrammarBuilder<int> Builder() => DummyBuilderTyped<int>();

        [PrecompilerOutput]
        public static CharParser<object> Parser() => null!;

        [PrecompilerOutput]
        public static CharParser<int> Valid() => null!;

        [PrecompilerOutput(SyntaxCheck = true)]
        public static CharParser<object> Valid_2() => null!;

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder<string?> Builder2() => DummyBuilderTyped<string>();

        [PrecompilerOutput(Key = "1")]
        public static CharParser<AssemblyName> Parser2() => null!;

        [PrecompilerOutput(Key = "1")]
        public static CharParser<string?> Valid2() => null!;

        [PrecompilerOutput(Key = "1")]
        public static CharParser<object?> Valid2_2() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0013", "FARKLE0013", "FARKLE0013"], ExpectedGrammars = 0)]
    private static class Failure_DuplicateKey
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder() => DummyBuilder;

        [PrecompilerInput]
        public static IGrammarBuilder Builder_2() => DummyBuilder;

        [PrecompilerInput]
        public static IGrammarBuilder Builder_3() => DummyBuilder;

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder Builder2() => DummyBuilder;

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder Builder2_2() => DummyBuilder;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0014", "FARKLE0014"], ExpectedOutputMethods = 0)]
    private static class Failure_MissingInputMethod
    {
        [PrecompilerOutput]
        public static Grammar Grammar() => null!;

        [PrecompilerOutput(Key = "1")]
        public static Grammar Grammar2() => null!;
    }

    [DiscovererTestCase(ExpectedDiagnostics = ["FARKLE0015"], ExpectedGrammars = 1, ExpectedOutputMethods = 1)]
    private static class Failure_Exception
    {
        [PrecompilerInput]
        public static IGrammarBuilder Builder() => throw new InvalidOperationException("Test exception");

        [PrecompilerOutput]
        public static Grammar Grammar() => null!;

        [PrecompilerInput(Key = "1")]
        public static IGrammarBuilder Valid() => DummyBuilder;

        [PrecompilerOutput(Key = "1")]
        public static CharParser<string?> ValidParser() => null!;
    }

    private static IGrammarBuilder DummyBuilder => Terminal.NewLine;

    private static IGrammarBuilder<T?> DummyBuilderTyped<T>() => Nonterminal.Create("S", Terminal.Literal("a").Appended().FinishConstant(default(T)));

    private static Builder.Untyped.Nonterminal DummyNonterminal => (Builder.Untyped.Nonterminal)Nonterminal.CreateUntyped("S", Terminal.Literal("a").Appended());

    private static Nonterminal<T> DummyNonterminalTyped<T>() => (Nonterminal<T>)Nonterminal.Create("S", Terminal.Literal("a").Appended().FinishConstant(default(T)));

    private static IEnumerable<object[]> DiscovererTestCases() =>
        from nestedType in typeof(PrecompilerInterfaceTests).GetNestedTypes(BindingFlags.NonPublic)
        let attr = nestedType.GetCustomAttribute<DiscovererTestCaseAttribute>()
        where attr != null
        let expectedGrammars = attr.ExpectedGrammars >= 0 ? attr.ExpectedGrammars : nestedType.GetMethods().Count(m => m.IsDefined(typeof(PrecompilerInputAttribute)))
        let expectedOutputMethods = attr.ExpectedOutputMethods >= 0 ? attr.ExpectedOutputMethods : nestedType.GetMethods().Count(m => m.IsDefined(typeof(PrecompilerOutputAttribute)))
        select (object[])[nestedType, attr.ExpectedDiagnostics, expectedGrammars, expectedOutputMethods];

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class DiscovererTestCaseAttribute : Attribute
    {
        public string[] ExpectedDiagnostics { get; set; } = [];

        public int ExpectedGrammars { get; set; } = -1;

        public int ExpectedOutputMethods { get; set; } = -1;
    }

    private sealed class PrecompilerOptions(BuilderLogger log) : IPrecompilerOptions, ILogger
    {
        public CancellationToken CancellationToken => CancellationToken.None;

        public ILogger Logger => this;

        public DiagnosticSeverity LogLevel => (DiagnosticSeverity)log.LogLevel;

        public void Log(DiagnosticSeverity severity, object message, string? code)
        {
            log.Log((Diagnostics.DiagnosticSeverity)severity, message, code);
        }
    }
}
