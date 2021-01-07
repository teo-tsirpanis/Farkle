// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Scriban.Runtime

[<RequireQualifiedAccess>]
type Language =
    | ``F#``
    | ``C#``

type GrammarTemplateInput = {
    Grammar: Grammar
    GrammarPath: string
}

type WebsiteOptions = unit

type TemplateType =
    | GrammarWebsite of GrammarTemplateInput * WebsiteOptions
    | GrammarSkeleton of GrammarTemplateInput * Language * ``namespace``: string option
    | GrammarCustomTemplate of GrammarTemplateInput * templatePath: string
    | LALRConflictReport

type TemplateOptions = {
    CustomProperties: (string * string) list
}

type GeneratedTemplate = {
    ScriptObject: IScriptObject
    Content: string
}
with
    member x.FileExtension =
        match x.ScriptObject.TryGetValue "file_extension" with
        | true, ext -> ext.ToString()
        | false, _ -> ".out.txt"
