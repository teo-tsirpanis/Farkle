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

namespace global

open System
open System.Runtime.CompilerServices

/// This attribute is used by Covarsky.
[<AttributeUsage(AttributeTargets.GenericParameter)>]
type internal CovariantOutAttribute() =
    inherit Attribute()

[<``module``:NullableContext(1uy)>]
[<assembly:Extension>]
do()
