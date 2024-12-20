// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammars

type GrammarTemplateInput = {
    Grammar: Grammar
    GrammarPath: string
}
with
    static member Create grammar grammarPath =
        {Grammar = grammar; GrammarPath = grammarPath}

type HtmlOptions = {
    CustomHeadContent: string
    NoCss: bool
    NoLALRStates: bool
    NoDFAStates: bool
}
with
    static let ``default`` = {CustomHeadContent = ""; NoCss = false; NoLALRStates = false; NoDFAStates = false}

    static member Default = ``default``

type CustomTemplateOptions = {
    AdditionalProperties: (string * string) list
}

type TemplateType =
    | GrammarHtml of GrammarTemplateInput * HtmlOptions
    | GrammarCustomTemplate of GrammarTemplateInput * templatePath: string * CustomTemplateOptions

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}
