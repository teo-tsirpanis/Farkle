// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Templating.BuiltinTemplates

open System.IO
open System.Reflection

[<RequireQualifiedAccess>]
type Language =
    | ``F#``
    member x.FullName =
        match x with
        | ``F#`` -> "FSharp"
    member x.FileName =
        match x with
        | ``F#`` -> "F# internal template"

[<RequireQualifiedAccess>]
type TemplateType =
    | Grammar

// The folder had a dash, not an underscore!
let private builtinsFolder = "builtin_scripts"

let private fetchResource (typ: TemplateType) lang =
    let resourceName = sprintf "Farkle.Tools.%s.%A.%s.scriban" builtinsFolder typ lang
    let assembly = Assembly.GetExecutingAssembly()
    let resourceStream = assembly.GetManifestResourceStream(resourceName) |> Option.ofObj
    match resourceStream with
    | Some resourceStream ->
        use sr = new StreamReader(resourceStream)
        sr.ReadToEnd()
    | None -> failwithf "Cannot find resource name '%s' inside the assembly." resourceName

let getLanguageTemplate typ (lang: Language) = fetchResource typ lang.FullName, lang.FileName
