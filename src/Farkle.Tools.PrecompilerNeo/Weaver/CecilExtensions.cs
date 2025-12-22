// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;

namespace Farkle.Tools.Precompiler.Weaver;

internal static class CecilExtensions
{
    public static MethodReference GetDelegateConstructor(this TypeReference @delegate)
    {
        var typeSystem = @delegate.Module.TypeSystem;
        var ctor = new MethodReference(".ctor", typeSystem.Void, @delegate);
        ctor.Parameters.Add(new(typeSystem.IntPtr));
        ctor.Parameters.Add(new(typeSystem.Object));
        return ctor;
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
}
