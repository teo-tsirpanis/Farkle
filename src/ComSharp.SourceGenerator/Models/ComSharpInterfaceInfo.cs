// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public sealed record InterfaceInfo(string Name, InterfaceAttributes Attributes, EquatableArray<MemberInfo> Members);

public abstract record MemberInfo(string Name);

public sealed record MethodInfo(string Name, ParameterInfo? ReturnType, EquatableArray<ParameterInfo> Parameters) : MemberInfo(Name)
{
    public bool NeedsMethodBody(bool marshalToComSharp)
    {
        if (ReturnType is { MarshallingInfo: ParameterMarshallingInfo.Tuple })
        {
            return true;
        }
        if (!marshalToComSharp && Parameters.Any(p => p is { MarshallingInfo: ParameterMarshallingInfo.ByReference { Kind: RefKind.Out } }))
        {
            return true;
        }
        return false;
    }
}

public sealed record PropertyInfo(string Name, ParameterInfo Type, PropertyAccessors Accessors, EquatableArray<ParameterInfo> IndexerParameters) : MemberInfo(Name)
{
    public bool HasGetter => (Accessors & PropertyAccessors.Get) != 0;

    public bool HasSetter => (Accessors & PropertyAccessors.Set) != 0;

    public bool NeedsGetterBody(bool marshalToComSharp)
    {
        if (Type.MarshallingInfo is ParameterMarshallingInfo.Tuple)
        {
            return true;
        }
        if (!marshalToComSharp && IndexerParameters.Any(p => p is { MarshallingInfo: ParameterMarshallingInfo.ByReference { Kind: RefKind.Out } }))
        {
            return true;
        }
        return false;
    }
}

// Events always have both add and remove accessors.
public sealed record EventInfo(string Name, string TypeName, ParameterMarshallingInfo MarshallingInfo) : MemberInfo(Name);

[Flags]
public enum PropertyAccessors
{
    Get = 1,
    Set = 2,
}

public sealed record ParameterInfo(string Name, string TypeName, ParameterMarshallingInfo MarshallingInfo)
{
    public RefKind RefKind => (MarshallingInfo as ParameterMarshallingInfo.ByReference)?.Kind ?? RefKind.None;

    public string GetMarshalledParameterType(bool marshalToComSharp) => marshalToComSharp ? MarshallingInfo.GetComSharpType(TypeName) : TypeName;
}

[Flags]
public enum InterfaceAttributes
{
    None = 0,
    ContainsBaseInterfacesWithMembers = 1,
}
