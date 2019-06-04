// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tools.Common

open System.Reflection

let toolsVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()