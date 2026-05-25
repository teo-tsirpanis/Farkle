// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;
using Mono.Cecil.Rocks;
using Sigourney;

namespace Farkle.Tools.Precompiler.Weaver;

internal sealed class KnownMembers(IReadOnlyCollection<AssemblyReference> references, ModuleDefinition module)
{
    private readonly Dictionary<string, AssemblyReference> _assemblyCache = references.ToDictionary(x => x.AssemblyName.Name, StringComparer.Ordinal);

    private IMetadataScope FindAssembly(ReadOnlySpan<string> assemblyNames)
    {
        foreach (var asmName in assemblyNames)
        {
            if (_assemblyCache.TryGetValue(asmName, out var reference))
            {
                return reference.AssemblyName;
            }
        }
        return module;
    }

    /// <summary>
    /// Searches for a type with the specified name in the given assembly references, or the input module.
    /// Returns null if the type is not found.
    /// </summary>
    private TypeReference? TryGetType(string @namespace, string name, ReadOnlySpan<string> assemblyNames)
    {
        TypeDefinition? result = null;
        foreach (var asmName in assemblyNames)
        {
            if (!_assemblyCache.TryGetValue(asmName, out var reference))
            {
                continue;
            }
            AssemblyDefinition asm = module.AssemblyResolver.Resolve(reference.AssemblyName);
            if (asm.MainModule.GetType(@namespace, name) is { } type)
            {
                result = type;
                break;
            }
        }
        result ??= module.GetType(@namespace, name);
        return module.ImportReference(result);
    }

    private TypeReference GetType(string @namespace, string name, ReadOnlySpan<string> assemblyNames) =>
        TryGetType(@namespace, name, assemblyNames)
        ?? throw new InvalidOperationException($"Missing required type {@namespace}.{name}");

    /// <summary>
    /// Returns a <see cref="TypeReference"/> to the specified type.
    /// </summary>
    /// <remarks>
    /// The type must be assumed to exist, otherwise a type load exception will be thrown when running the weaved
    /// assembly. For this reason, it should only be used for types that are known to be present, like Farkle's own
    /// types. For system types, use <see cref="TryGetType"/> instead.
    /// </remarks>
    private TypeReference MakeTypeReference(string @namespace, string name, IMetadataScope scope, bool isValueType = false, int genericParameterCount = 0)
    {
        // Set module to null and import later, in order to canonicalize the
        // scope, and prevent mysterious exceptions when writing.
        var result = new TypeReference(@namespace, name, null, scope, isValueType);
        for (int i = 0; i < genericParameterCount; i++)
        {
            result.GenericParameters.Add(new(result));
        }
        return module.ImportReference(result);
    }

    private static T? CheckIfExists<T>(T? member, ref CheckedState checkedState) where T : MemberReference
    {
        switch (checkedState)
        {
            case CheckedState.NotChecked:
                if (member?.Resolve() is null)
                {
                    checkedState = CheckedState.DoesNotExist;
                    return null;
                }
                checkedState = CheckedState.Exists;
                return member;
            case CheckedState.Exists:
                return member;
            default:
                return null;
        }
    }

    private static string[] CoreLib { get; } = ["System.Runtime", "netstandard", "mscorlib"];

    public TypeReference Byte => module.TypeSystem.Byte;

    public TypeReference String => module.TypeSystem.String;

    public TypeReference Void => module.TypeSystem.Void;

    public TypeReference DebuggerStepThroughAttribute => field ??=
        GetType("System.Diagnostics", "DebuggerStepThroughAttribute", CoreLib);

    public MethodReference DebuggerStepThroughAttribute_Ctor => field ??=
        DebuggerStepThroughAttribute.MakeMethodReference(true, ".ctor", Void, []);

    public TypeReference Func_1 => field ??=
        GetType("System", "Func`1", CoreLib);

    public TypeReference GeneratedCodeAttribute => field ??=
        GetType("System.CodeDom.Compiler", "GeneratedCodeAttribute", CoreLib);

    public MethodReference GeneratedCodeAttribute_Ctor => field ??=
        GeneratedCodeAttribute.MakeMethodReference(true, ".ctor", Void, [String, String]);

    public TypeReference RuntimeTypeHandle => field ??=
        GetType("System", "RuntimeTypeHandle", CoreLib);

    public TypeReference ValueType => field ??=
        GetType("System", "ValueType", CoreLib);

    private static string[] SystemRuntimeLoader { get; } = ["System.Runtime.Loader"];

    public TypeReference MetadataUpdater => field ??=
        GetType("System.Reflection.Metadata", "MetadataUpdater", SystemRuntimeLoader);

    public MethodReference MetadataUpdater_get_IsSupported => field ??=
        MetadataUpdater.MakeMethodReference(false, "get_IsSupported", module.TypeSystem.Boolean, []);

    public IMetadataScope Farkle => field ??=
        FindAssembly(["Farkle"]);

    public TypeReference CharParser_1 => field ??=
        MakeTypeReference("Farkle", "CharParser`1", Farkle, genericParameterCount: 1);

    public TypeReference Grammar => field ??=
        MakeTypeReference("Farkle.Grammars", "Grammar", Farkle);

    public TypeReference IGrammarBuilder => field ??=
        MakeTypeReference("Farkle.Builder", "IGrammarBuilder", Farkle);

    public TypeReference IGrammarBuilder_1 => field ??=
        MakeTypeReference("Farkle.Builder", "IGrammarBuilder`1", Farkle, genericParameterCount: 1);

    public TypeReference PrecompilerEntryPoints => field ??=
        MakeTypeReference("Farkle.Runtime", "PrecompilerEntryPoints", Farkle);

    private TypeReference[] LoadGrammarPreamble => field ??=
        [Byte.MakePointerType(), module.TypeSystem.Int32, RuntimeTypeHandle];

    public TypeReference PrecompiledGrammarAttribute => field ??=
        MakeTypeReference("Farkle.Runtime", "PrecompiledGrammarAttribute", Farkle);

    public MethodReference PrecompiledGrammarAttribute_Ctor => field ??=
        PrecompiledGrammarAttribute.MakeMethodReference(true, ".ctor", Void, []);

    public MethodReference PrecompilerEntryPoints_LoadGrammar => field ??=
        PrecompilerEntryPoints.MakeMethodReference(false, "LoadGrammar", Grammar, LoadGrammarPreamble);

    public MethodReference PrecompilerEntryPoints_LoadCharParser
    {
        get
        {
            return field ??= CreateValue();

            MethodReference CreateValue()
            {
                var result = new MethodReference("LoadCharParser", Void, PrecompilerEntryPoints);
                var genericParam = new GenericParameter(result);
                result.GenericParameters.Add(genericParam);
                result.ReturnType = CharParser_1.MakeGenericInstanceType(genericParam);
                foreach (var t in LoadGrammarPreamble)
                {
                    result.Parameters.Add(new(t));
                }
                result.Parameters.Add(new(Func_1.MakeGenericInstanceType(IGrammarBuilder_1.MakeGenericInstanceType(genericParam))));
                return result;
            }
        }
    }

    public MethodReference PrecompilerEntryPoints_LoadCharParserSyntaxChecker
    {
        get
        {
            return field ??= CreateValue();

            MethodReference CreateValue()
            {
                var result = new MethodReference("LoadCharParserSyntaxChecker", Void, PrecompilerEntryPoints);
                var genericParam = new GenericParameter(result);
                result.GenericParameters.Add(genericParam);
                result.ReturnType = CharParser_1.MakeGenericInstanceType(genericParam);
                foreach (var t in LoadGrammarPreamble)
                {
                    result.Parameters.Add(new(t));
                }
                result.Parameters.Add(new(Func_1.MakeGenericInstanceType(IGrammarBuilder)));
                return result;
            }
        }
    }

    private enum CheckedState : byte
    {
        NotChecked,
        Exists,
        DoesNotExist,
    }
}
