// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;

namespace Farkle.Tools.Precompiler.Weaver;

internal static class CecilExtensions
{
    public static AssemblyNameReference GetAssemblyName(this IMetadataScope scope) =>
        scope as AssemblyNameReference ?? ((ModuleDefinition) scope).Assembly.Name;

    public static MethodReference GetDelegateConstructor(this TypeReference @delegate)
    {
        var typeSystem = @delegate.Module.TypeSystem;
        return @delegate.MakeMethodReference(true, ".ctor", typeSystem.Void, [typeSystem.Object, typeSystem.IntPtr]);
    }

    public static GenericInstanceMethod MakeGenericMethod(this MethodReference method, ReadOnlySpan<TypeReference> typeArguments)
    {
        var result = new GenericInstanceMethod(method);
        foreach (var t in typeArguments)
        {
            result.GenericArguments.Add(t);
        }
        return result;
    }

    public static MethodReference MakeMethodReference(this TypeReference type, bool isInstance, string name,
        TypeReference returnType, ReadOnlySpan<TypeReference> parameterTypes)
    {
        var result = new MethodReference(name, returnType, type)
        {
            HasThis = isInstance
        };
        foreach (var p in parameterTypes)
        {
            result.Parameters.Add(new(p));
        }

        return result;
    }

    public static PointerType MakePointerType(this TypeReference type) => new(type);
}
