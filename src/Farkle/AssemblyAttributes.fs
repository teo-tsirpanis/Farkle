// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace global

open System.Runtime.CompilerServices

[<assembly:Extension>]
[<assembly:InternalsVisibleTo("Farkle.Tests")>]
[<assembly:InternalsVisibleTo("Farkle.Tools")>]
do()
