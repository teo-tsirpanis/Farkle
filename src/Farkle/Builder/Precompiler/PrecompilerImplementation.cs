// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ComSharp;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;

namespace Farkle.Builder.Precompiler;

/// <summary>
/// Contains logic to discover and build precompiled grammars.
/// </summary>
[RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
internal sealed class PrecompilerImplementation : IPrecompilerInterface
{
    internal const string RequiresUnreferencedCodeMessage = "Methods that are searched by the precompiler might be removed.";

    public IEnumerable<IPrecompiledGrammar> DiscoverAndPrecompile(Assembly assembly, IPrecompilerOptions? options)
    {
        CancellationToken ct = options?.CancellationToken ?? CancellationToken.None;
        BuilderLogger log = CreateBuilderLogger(options?.Logger);
        CandidateGrammarDictionary candidateGrammars = new();
        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                ct.ThrowIfCancellationRequested();
                if (method.GetCustomAttribute<PrecompilerInputAttribute>() is { } inputAttribute)
                {
                    candidateGrammars.GetOrAdd(inputAttribute.Key).AddInputMethod(method, inputAttribute, in log);
                }
                if (method.GetCustomAttribute<PrecompilerOutputAttribute>() is { } outputAttribute)
                {
                    candidateGrammars.GetOrAdd(outputAttribute.Key).AddOutputMethod(method, outputAttribute, in log);
                }
            }

            foreach (var x in candidateGrammars)
            {
                if (x.Precompile(in log, ct) is { } grammar)
                {
                    yield return grammar;
                }
            }
            candidateGrammars.Clear();
        }
    }

    private static BuilderLogger CreateBuilderLogger(ILogger? logger)
    {
        BuilderLogger builderLogger = new();

        if (logger is not null)
        {
            builderLogger.LogLevel = (Diagnostics.DiagnosticSeverity)logger.LogLevel;
            builderLogger.OnDiagnostic += d => logger.Log((DiagnosticSeverity)d.Severity, d.Message, d.Code);
        }

        return builderLogger;
    }

    private sealed class CandidateGrammar
    {
        private MethodInfo? _inputMethod;
        private PrecompilerInputAttribute? _inputAttribute;
        private bool _canCallInputMethod;
        private Type? _grammarBuilderReturnType;

        private readonly List<(MethodInfo Method, PrecompilerOutputAttribute Attribute)> _outputMethods = [];

        public void AddInputMethod(MethodInfo method, PrecompilerInputAttribute inputAttribute, in BuilderLogger log)
        {
            if (_inputMethod is not null)
            {
                // TODO: Log error
                // Even if we've found an eligible input method before, make the final validation immediately fail,
                // and don't try to build the grammar.
                _canCallInputMethod = false;
                return;
            }
            _inputMethod = method;
            _inputAttribute = inputAttribute;
            _canCallInputMethod = true;
            // DeclaringType will always be non-null, because we got the method from a type.
            if (method.IsGenericMethod || method.DeclaringType!.IsGenericType)
            {
                // TODO: Log error
                _canCallInputMethod = false;
            }
            if (!method.IsStatic)
            {
                // TODO: Log error
                _canCallInputMethod = false;
            }
            if (method.GetParameters().Length > 0)
            {
                // TODO: Log error
                _canCallInputMethod = false;
            }
            if (!method.ReturnType.IsAssignableTo(typeof(IGrammarBuilder)))
            {
                // TODO: Log error
                _canCallInputMethod = false;
            }
            if (!_canCallInputMethod)
            {
                return;
            }
            // This will throw if the input method returns a type that implements IGrammarBuilder<T> more than once,
            // but we aren't doing that, and this interface cannot be implemented by user code.
            _grammarBuilderReturnType =
                GetInterfacesWithSameMetadataDefinitionAs(method.ReturnType, typeof(IGrammarBuilder<>))
                .Select(t => t.GetGenericArguments()[0])
                .SingleOrDefault();
        }

        public void AddOutputMethod(MethodInfo method, PrecompilerOutputAttribute outputAttribute, in BuilderLogger log)
        {
            bool eligible = true;
            // DeclaringType will always be non-null, because we got the method from a type.
            if (method.IsGenericMethod || method.DeclaringType!.IsGenericType)
            {
                // TODO: Log error
                eligible = false;
            }
            if (!method.IsStatic)
            {
                // TODO: Log error
                eligible = false;
            }
            if (method.GetParameters().Length > 0)
            {
                // TODO: Log error
                eligible = false;
            }
            if (!IsEligibleOutputMethodReturnType(method.ReturnType))
            {
                // TODO: Log error
                eligible = false;
            }
            if (!eligible)
            {
                return;
            }
            _outputMethods.Add((method, outputAttribute));
        }

        private List<(int MetadataToken, OutputType Type)> GetOutputMethods(in BuilderLogger log)
        {
            var result = new List<(int, OutputType)>(_outputMethods.Count);
            foreach (var x in _outputMethods)
            {
                if (!IsCompatibleOutputMethodReturnType(x.Method.ReturnType, x.Attribute.SyntaxCheck, out OutputType outputType))
                {
                    // TODO: Log error
                    continue;
                }
                result.Add((x.Method.MetadataToken, outputType));
            }
            return result;
        }

        public IPrecompiledGrammar? Precompile(in BuilderLogger log, CancellationToken ct)
        {
            if (_inputMethod is null)
            {
                // TODO: Log error
                return null;
            }
            if (!_canCallInputMethod)
            {
                return null;
            }
            Debug.Assert(_inputAttribute is not null);
            var outputMethods = GetOutputMethods(in log);
            var builderOptions = new BuilderOptions { CancellationToken = ct, Log = log };
            builderOptions.UpdateFrom(_inputAttribute);
            IGrammarBuilder? builderObject;
            try
            {
                builderObject = (IGrammarBuilder?)_inputMethod.Invoke(null, null);
            }
            catch (TargetInvocationException)
            {
                // TODO: Log error
                return null;
            }
            if (builderObject is null)
            {
                // TODO: Log error
                return null;
            }
            // TODO: Log info "Precompiling {grammarName}..."
            var output = builderObject.BuildSyntaxCheck(BuilderOutputs.GrammarDfaOnChar | BuilderOutputs.GrammarLrStateMachine, builderOptions);
            return new PrecompiledGrammar
            {
                Key = _inputAttribute.Key,
                GrammarFile = output.Grammar!.ToImmutableArray(),
                InputMethodMetadataToken = _inputMethod.MetadataToken,
                OutputMethods = outputMethods,
            };
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "We are searching for a user-provided interface type that should be available.")]
        private static Type[] GetInterfacesWithSameMetadataDefinitionAs(Type type, Type interfaceType) =>
            type.FindInterfaces(static (t, obj) => t.HasSameMetadataDefinitionAs((MemberInfo)obj!), interfaceType);

        private static bool IsEligibleOutputMethodReturnType(Type type) =>
            type.IsAssignableTo(typeof(Grammar))
            || type.HasSameMetadataDefinitionAs(typeof(CharParser<>));

        private bool IsCompatibleOutputMethodReturnType(Type type, bool isForcedSyntaxCheck, out OutputType outputType)
        {
            outputType = OutputType.Grammar;
            if (type == typeof(Grammar))
            {
                return true;
            }
            if (type.HasSameMetadataDefinitionAs(typeof(CharParser<>)))
            {
                var parserReturnType = type.GetGenericArguments()[0];
                if (isForcedSyntaxCheck || _grammarBuilderReturnType is null)
                {
                    outputType = OutputType.CharParserSyntaxChecker;
                    return !parserReturnType.IsValueType;
                }
                outputType = OutputType.CharParser;
                // We could check for nullability here, but we don't have enough information on whether
                // nullable warnings are enabled or not. Better have an analyzer do it.
                return _grammarBuilderReturnType.IsAssignableTo(parserReturnType);
            }
            return false;
        }
    }

    private sealed class PrecompiledGrammar : IPrecompiledGrammar
    {
        public required string? Key { get; init; }

        public required ImmutableArray<byte> GrammarFile { get; init; }

        public required int InputMethodMetadataToken { get; init; }

        public required IReadOnlyList<(int MetadataToken, OutputType Type)> OutputMethods { get; init; }
    }

    private struct CandidateGrammarDictionary
    {
        private CandidateGrammar? _defaultGrammar;
        private Dictionary<string, CandidateGrammar>? _namedGrammars;

        public void Clear()
        {
            _defaultGrammar = null;
            _namedGrammars?.Clear();
        }

        public readonly IEnumerator<CandidateGrammar> GetEnumerator()
        {
            if (_defaultGrammar is not null)
            {
                yield return _defaultGrammar;
            }
            if (_namedGrammars is not null)
            {
                foreach (var x in _namedGrammars.Values)
                {
                    yield return x;
                }
            }
        }

        public CandidateGrammar GetOrAdd(string? key)
        {
            if (key is null)
            {
                return _defaultGrammar ??= new CandidateGrammar();
            }
            _namedGrammars ??= [];
            if (!_namedGrammars.TryGetValue(key, out var grammar))
            {
                grammar = new CandidateGrammar();
                _namedGrammars.Add(key, grammar);
            }
            return grammar;
        }
    }
}
