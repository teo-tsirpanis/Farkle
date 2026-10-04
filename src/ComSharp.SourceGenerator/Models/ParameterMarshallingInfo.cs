// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

public abstract record ParameterMarshallingInfo
{
    public abstract string GetComSharpType(string dotNetType);

    public sealed record Identity : ParameterMarshallingInfo
    {
        public static Identity Instance { get; } = new();

        public override string GetComSharpType(string dotNetType) => dotNetType;
    }

    public sealed record SimpleCast(string ComSharpType) : ParameterMarshallingInfo
    {
        public override string GetComSharpType(string dotNetType) => ComSharpType;
    };

    public sealed record ComSharpInterface : ParameterMarshallingInfo
    {
        public static ComSharpInterface Instance { get; } = new();

        public override string GetComSharpType(string dotNetType) => "ComSharpObject";
    }

    public sealed record Tuple(bool IsValueTuple, EquatableArray<string> ElementTypeNames,
        EquatableArray<ParameterMarshallingInfo> ElementMarshallingInfos) : ParameterMarshallingInfo
    {
        public override string GetComSharpType(string dotNetType)
        {
            var elements = string.Join(", ", ElementMarshallingInfos.Zip(ElementTypeNames, (info, typeName) => info.GetComSharpType(typeName)));
            return IsValueTuple ? $"({elements})" : $"global::System.Tuple<{elements}>";
        }
    }

    public sealed record ByReference(RefKind Kind) : ParameterMarshallingInfo
    {
        public override string GetComSharpType(string dotNetType) => Kind switch
        {
            RefKind.In or RefKind.RefReadOnlyParameter => $"global::System.ReadOnlySpan<{dotNetType}>",
            _ => $"global::System.Span<{dotNetType}>",
        };
    }

    public sealed record GenericCollection(CollectionType Collection, string ElementTypeName, ParameterMarshallingInfo ElementMarshallingInfo) : ParameterMarshallingInfo
    {
        public bool IsReadOnly => Collection is not (CollectionType.ICollection or CollectionType.IList);

        public override string GetComSharpType(string dotNetType)
        {
            return $"global::System.Collections.Generic.{Collection}<{ElementMarshallingInfo.GetComSharpType(ElementTypeName)}>";
        }
    }
}

public enum CollectionType
{
    IEnumerable,
    IEnumerator,
    ICollection,
    IList,
    IReadOnlyCollection,
    IReadOnlyList,
    IAsyncEnumerable,
    IAsyncEnumerator,
}

public sealed record TupleElementMarshallingInfo(string Name, string TypeName, ParameterMarshallingInfo MarshallingInfo);
