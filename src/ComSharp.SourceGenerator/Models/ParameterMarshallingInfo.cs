// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace ComSharp;

public abstract record ParameterMarshallingInfo
{
    public sealed record Identity : ParameterMarshallingInfo
    {
        public static Identity Instance { get; } = new();
    }

    public sealed record SimpleCast(string ComSharpType) : ParameterMarshallingInfo;

    public sealed record ComSharpInterface : ParameterMarshallingInfo
    {
        public static ComSharpInterface Instance { get; } = new();
    }

    public sealed record Tuple(bool IsValueTuple, EquatableArray<ParameterMarshallingInfo> Elements) : ParameterMarshallingInfo;

    public sealed record ByReference : ParameterMarshallingInfo
    {
        public static ByReference Instance { get; } = new();
    }

    public sealed record GenericCollection(ParameterMarshallingInfo ElementType, bool IsReadWrite) : ParameterMarshallingInfo;
}
