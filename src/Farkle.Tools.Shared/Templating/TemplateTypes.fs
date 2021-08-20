// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Builder
open Farkle.Grammar
open System.Collections.Immutable

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

type HtmlOptions = {
    CustomHeadContent: string
    NoCss: bool
    NoLALRStates: bool
    NoDFAStates: bool
}
with
    static member Default =
        {CustomHeadContent = ""; NoCss = true; NoLALRStates = false; NoDFAStates = true}

type CustomTemplateOptions = {
    AdditionalProperties: (string * string) list
}

type TemplateType =
    | GrammarHtml of GrammarTemplateInput * HtmlOptions
    | GrammarSkeleton of GrammarTemplateInput * Language * ``namespace``: string option
    | GrammarCustomTemplate of GrammarTemplateInput * templatePath: string * CustomTemplateOptions
    | LALRConflictReport of GrammarDefinition * LALRConflictState ImmutableArray

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}
