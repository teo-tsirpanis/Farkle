// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace global

open System
open System.Runtime.CompilerServices

[<AttributeUsage(AttributeTargets.GenericParameter)>]
type internal CovariantOutAttribute() =
    inherit Attribute()

[<assembly:Extension>]
[<assembly:InternalsVisibleTo("Farkle.Benchmarks")>]
[<assembly:InternalsVisibleTo("Farkle.Tests")>]
[<assembly:InternalsVisibleTo("Farkle.Tools")>]
[<assembly:InternalsVisibleTo("Farkle.Tools.Shared")>]
[<assembly:InternalsVisibleTo("Farkle.Tools.MSBuild")>]
do()
