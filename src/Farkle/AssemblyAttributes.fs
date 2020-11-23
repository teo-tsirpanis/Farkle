// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices

open System
open System.ComponentModel

[<AttributeUsage(AttributeTargets.All, Inherited = false)>]
[<EditorBrowsable(EditorBrowsableState.Never)>]
type internal IsReadOnlyAttribute() = inherit Attribute()
#endif

namespace global

open System
open System.Runtime.CompilerServices

/// This attribute is used by Covarsky.
[<AttributeUsage(AttributeTargets.GenericParameter)>]
type internal CovariantOutAttribute() =
    inherit Attribute()

[<assembly:Extension>]
do()
