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
    | ``C#``

let private (|LanguageNames|) lang =
    match lang with
    | Language.``F#`` -> "FSharp", "F# internal template"
    | Language.``C#`` -> "CSharp", "C# internal template"

[<RequireQualifiedAccess>]
type TemplateType =
    | Grammar
    | PostProcessor

type TemplateSource =
    | CustomFile of string
    | BuiltinTemplate of Language * TemplateType


// The folder had a dash, not an underscore!
let private builtinsFolder = "builtin_scripts"

let private fetchResource (typ: TemplateType) lang =
    let assembly = Assembly.GetExecutingAssembly()
    let resourceName = sprintf "%s.%s.%A.%s.scriban" (assembly.GetName().Name) builtinsFolder typ lang
    let resourceStream = assembly.GetManifestResourceStream(resourceName) |> Option.ofObj
    match resourceStream with
    | Some resourceStream ->
        use sr = new StreamReader(resourceStream)
        sr.ReadToEnd()
    | None -> failwithf "Cannot find resource name '%s' inside the assembly." resourceName

let getLanguageTemplate x =
    match x with
    | CustomFile path -> File.ReadAllText path, path
    | BuiltinTemplate(LanguageNames(fullName, templateFileName), typ) -> fetchResource typ fullName, templateFileName
