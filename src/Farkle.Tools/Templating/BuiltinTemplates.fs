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

[<RequireQualifiedAccess>]
type TemplateType =
    | Grammar

[<RequireQualifiedAccess>]
type private TemplateInternalLanguage =
    | FSharp

// The folder had a dash, not an underscore!
let private builtinsFolder = "builtin_scripts"

let private fetchResource (typ: TemplateType) (lang: TemplateInternalLanguage) =
    let resourceName = sprintf "Farkle.Tools.%s.%A.%A.scriban" builtinsFolder typ lang
    let assembly = Assembly.GetExecutingAssembly()
    let resourceStream = assembly.GetManifestResourceStream(resourceName) |> Option.ofObj
    match resourceStream with
    | Some resourceStream ->
        use sr = new StreamReader(resourceStream)
        sr.ReadToEnd()
    | None -> failwithf "Cannot find resource name '%s' inside the assembly." resourceName

let getLanguageTemplate typ lang =
    match lang with
    | Language.``F#`` -> fetchResource typ TemplateInternalLanguage.FSharp, "F# internal template"
