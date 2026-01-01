// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;
using Mono.Cecil.Rocks;
using Sigourney;

namespace Farkle.Tools.Precompiler.Weaver;

internal sealed class KnownMembers(IReadOnlyCollection<AssemblyReference> references, ModuleDefinition module)
{
    private IMetadataScope FindAssembly(ReadOnlySpan<string> assemblyNames)
    {
        foreach (var asmName in assemblyNames)
        {
            if (references.FirstOrDefault(x => x.AssemblyName.Name == asmName) is { } reference)
            {
                return reference.AssemblyName;
            }
        }
        return module;
    }

    private TypeReference GetType(string @namespace, string name, IMetadataScope scope, bool isValueType = false, int genericParameterCount = 0)
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

    private static T? CheckIfExists<T>(T member, ref CheckedState checkedState) where T : MemberReference
    {
        switch (checkedState)
        {
            case CheckedState.NotChecked:
                if (member.Resolve() is null)
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

    public IMetadataScope CoreLib => field ??=
        FindAssembly(["System.Runtime", "netstandard", "mscorlib"]);

    public TypeReference Byte => module.TypeSystem.Byte;

    public TypeReference String => module.TypeSystem.String;

    public TypeReference Void => module.TypeSystem.Void;

    public TypeReference DebuggerStepThroughAttribute => field ??=
        GetType("System.Diagnostics", "DebuggerStepThroughAttribute", CoreLib);

    public MethodReference DebuggerStepThroughAttribute_Ctor => field ??=
        DebuggerStepThroughAttribute.MakeMethodReference(true, ".ctor", Void, []);

    public TypeReference Func_1 => field ??=
        GetType("System", "Func`1", CoreLib, genericParameterCount: 1);

    public TypeReference GeneratedCodeAttribute => field ??=
        GetType("System.CodeDom.Compiler", "GeneratedCodeAttribute", CoreLib);

    public MethodReference GeneratedCodeAttribute_Ctor => field ??=
        GeneratedCodeAttribute.MakeMethodReference(true, ".ctor", Void, [String, String]);

    public TypeReference RuntimeTypeHandle => field ??=
        GetType("System", "RuntimeTypeHandle", CoreLib, isValueType: true);

    public TypeReference ValueType => field ??=
        GetType("System", "ValueType", CoreLib);

    public IMetadataScope SystemRuntimeLoader => field ??=
        FindAssembly(["System.Runtime.Loader"]);

    public TypeReference MetadataUpdater => field ??=
        GetType("System.Reflection.Metadata", "MetadataUpdater", SystemRuntimeLoader);

    private CheckedState MetadataUpdater_get_IsSupported_checkedState;

    public MethodReference? MetadataUpdater_get_IsSupported => CheckIfExists(field ??=
        MetadataUpdater.MakeMethodReference(false, "get_IsSupported", module.TypeSystem.Boolean, []),
        ref MetadataUpdater_get_IsSupported_checkedState);

    public IMetadataScope Farkle => field ??=
        FindAssembly(["Farkle"]);

    public TypeReference CharParser_1 => field ??=
        GetType("Farkle", "CharParser`1", Farkle, genericParameterCount: 1);

    public TypeReference Grammar => field ??=
        GetType("Farkle.Grammars", "Grammar", Farkle);

    public TypeReference IGrammarBuilder => field ??=
        GetType("Farkle.Builder", "IGrammarBuilder", Farkle);

    public TypeReference IGrammarBuilder_1 => field ??=
        GetType("Farkle.Builder", "IGrammarBuilder`1", Farkle, genericParameterCount: 1);

    public TypeReference PrecompilerEntryPoints => field ??=
        GetType("Farkle.Runtime", "PrecompilerEntryPoints", Farkle);

    private TypeReference[] LoadGrammarPreamble => field ??=
        [Byte.MakePointerType(), module.TypeSystem.Int32, RuntimeTypeHandle];

    public TypeReference PrecompiledGrammarAttribute => field ??=
        GetType("Farkle.Runtime", "PrecompiledGrammarAttribute", Farkle);

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
