// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar

[<RequireQualifiedAccess>]
type Language =
    | ``F#``
    | ``C#``

type GrammarTemplateInput = {
    Grammar: Grammar
    GrammarPath: string
}
with
    static member Create grammar grammarPath =
        {Grammar = grammar; GrammarPath = grammarPath}

type HtmlOptions = unit

type CustomTemplateOptions = {
    AdditionalProperties: (string * string) list
}

type TemplateType =
    | GrammarHtml of GrammarTemplateInput * HtmlOptions
    | GrammarSkeleton of GrammarTemplateInput * Language * ``namespace``: string option
    | GrammarCustomTemplate of GrammarTemplateInput * templatePath: string * CustomTemplateOptions
    | LALRConflictReport

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}
