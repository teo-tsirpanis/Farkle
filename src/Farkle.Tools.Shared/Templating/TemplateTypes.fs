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
    FileExtension: string
    Content: string
}
