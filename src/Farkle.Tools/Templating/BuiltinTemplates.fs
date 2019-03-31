// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Templating.BuiltinTemplates

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

// The folder had a dash, not an underscore!
let private builtinsFolder = "builtin_scripts"

let resolveInput x =
    match x with
    | ":" -> Console.In
    | x when x.StartsWith(":") ->
        let stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sprintf "Farkle.Tools.%s.%s.scriban" builtinsFolder <| x.Substring(1))
        new StreamReader(stream) :> TextReader
    | x -> File.OpenText x :> TextReader

let getAllBuiltins() =
    let assembly = Assembly.GetExecutingAssembly()
    let names = assembly.GetManifestResourceNames()
    let builtInRegex = Regex(@"Farkle\.Tools\." + builtinsFolder + "\.([\w\.]+)\.scriban", RegexOptions.Compiled)
    names
    |> Array.choose (fun x ->
        let res = builtInRegex.Match x
        if res.Success then
            Some res.Groups.[1].Value
        else
            None)
