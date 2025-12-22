// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;
using Sigourney;

namespace Farkle.Tools.Precompiler.Weaver;

internal sealed class KnownMembers(IReadOnlyCollection<AssemblyReference> references, ModuleDefinition module)
{
    private static readonly string[] _systemAssemblies = ["System.Runtime", "netstandard", "mscorlib"];

    private TypeDefinition? TryGetType(string @namespace, string name, ReadOnlySpan<string> assemblyNames)
    {
        foreach (var asmName in assemblyNames)
        {
            if (references.FirstOrDefault(x => x.AssemblyName.Name == asmName) is not { } reference)
            {
                continue;
            }
            AssemblyDefinition asm = module.AssemblyResolver.Resolve(reference.AssemblyName);
            if (asm.MainModule.GetType(@namespace, name) is { } type)
            {
                return type;
            }
        }
        return module.GetType(@namespace, name);
    }

    private TypeDefinition GetType(string @namespace, string name, ReadOnlySpan<string> assemblyNames) =>
        TryGetType(@namespace, name, assemblyNames)
        ?? throw new InvalidOperationException($"Missing required type {@namespace}.{name}");

    private TypeDefinition GetType(string @namespace, string name, AssemblyDefinition assembly) =>
        assembly.MainModule.Types.First(x => x.Namespace == @namespace && x.Name == name)
        ?? throw new InvalidOperationException($"Missing required type {@namespace}.{name}");

    public TypeReference String { get; } = module.TypeSystem.String;

    public TypeDefinition GeneratedCodeAttribute => field ??=
        GetType("System.CodeDom.Compiler", "GeneratedCodeAttribute", _systemAssemblies);

    public TypeDefinition Func_1 => field ??=
        GetType("System", "Func`1", _systemAssemblies);

    public MethodDefinition GeneratedCodeAttribute_Ctor => field ??=
        GeneratedCodeAttribute.Methods.First(x => !x.IsStatic
            && x.IsConstructor
            && x.Parameters is [var p1, var p2]
            && p1.ParameterType == String
            && p2.ParameterType == String);

    public AssemblyDefinition Farkle => field ??=
        references.FirstOrDefault(x => x.AssemblyName.Name is "Farkle") is { } farkleRef
            ? module.AssemblyResolver.Resolve(farkleRef.AssemblyName)
            // Farkle is embedded to the input assembly.
            : module.Assembly;

    public TypeDefinition IGrammarBuilder => field ??=
        GetType("Farkle.Builder", "IGrammarBuilder", Farkle);

    public TypeDefinition IGrammarBuilder_1 => field ??=
        GetType("Farkle.Builder", "IGrammarBuilder`1", Farkle);

    public TypeDefinition PrecompilerEntryPoints => field ??=
        GetType("Farkle.Runtime", "PrecompilerEntryPoints", Farkle);

    public MethodDefinition PrecompilerEntryPoints_LoadGrammar => field ??=
        PrecompilerEntryPoints.Methods.Single(x => x.Name == "LoadGrammar");

    public MethodDefinition PrecompilerEntryPoints_LoadCharParser => field ??=
        PrecompilerEntryPoints.Methods.Single(x => x.Name == "LoadCharParser");

    public MethodDefinition PrecompilerEntryPoints_LoadCharParserSyntaxChecker => field ??=
        PrecompilerEntryPoints.Methods.Single(x => x.Name == "LoadCharParserSyntaxChecker");

    public TypeDefinition PrecompiledGrammarAttribute => field ??=
        GetType("Farkle.Runtime", "PrecompiledGrammarAttribute", Farkle);

    public MethodDefinition PrecompiledGrammarAttribute_Ctor => field ??=
        PrecompiledGrammarAttribute.Methods.Single(x => !x.IsStatic && x.IsConstructor);
}
