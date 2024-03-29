// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Microsoft.CodeAnalysis

[<Embedded; System.Runtime.CompilerServices.CompilerGenerated>]
type internal EmbeddedAttribute() = inherit System.Attribute()

namespace System.Runtime.CompilerServices

open Microsoft.CodeAnalysis
open System

#if NETSTANDARD2_0
[<AttributeUsage(AttributeTargets.All, Inherited = false)>]
type internal IsReadOnlyAttribute() = inherit Attribute()

[<AttributeUsage(AttributeTargets.Struct, Inherited = false)>]
type internal IsByRefLikeAttribute() = inherit Attribute()
#endif

// The nullability annotation metadata syntax is documented in
// https://github.com/dotnet/roslyn/blob/master/docs/features/nullable-metadata.md

[<CompilerGenerated; Embedded>]
// Class, Property, Field, Event, Parameter, ReturnValue, GenericParameter
[<AttributeUsage(enum 0x6B84)>]
type internal NullableAttribute([<ParamArray>] __: byte[]) =
    inherit Attribute()
    new (flag) = NullableAttribute([|flag|])

[<CompilerGenerated; Embedded>]
// Module, Class, Struct, Method, Interface, Delegate
[<AttributeUsage(enum 0x144E)>]
type internal NullableContextAttribute(__: byte) = inherit Attribute()

[<CompilerGenerated; Embedded>]
[<AttributeUsage(AttributeTargets.Module)>]
type internal NullablePublicOnlyAttribute(__: bool) = inherit Attribute()

namespace global

open System
open System.Reflection
open System.Runtime.CompilerServices

/// This attribute is used by Covarsky.
[<AttributeUsage(AttributeTargets.GenericParameter)>]
type internal CovariantOutAttribute() =
    inherit Attribute()

[<``module``:NullableContext(1uy)>]
[<``module``:NullablePublicOnly(true)>]
[<assembly:Extension>]
#if NET
[<assembly:AssemblyMetadata("IsTrimmable", "true")>]
#else
[<assembly:AssemblyMetadata("IsTrimmable", "false")>]
#endif
do()
