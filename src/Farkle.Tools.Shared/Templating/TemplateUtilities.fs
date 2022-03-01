// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Tools
open Scriban
open Scriban.Runtime

type FarkleObject = {Version: string}

module internal Utilities =

    let private defaultFarkleObject = {Version = toolsVersion}

    let private builtinPrefix = "builtin://"

    let private htmlTemplateLoader = {new ITemplateLoader with
        member _.GetPath(_, _, templatePath) =
            if templatePath.StartsWith(builtinPrefix) then
                sprintf "Html.%s.scriban" (templatePath.Substring(builtinPrefix.Length))
            else
                null
        member _.Load(_, _, templatePath) =
            ResourceLoader.load templatePath
        member _.LoadAsync(_, _, templatePath) =
            ResourceLoader.load templatePath |> System.Threading.Tasks.ValueTask<_>}

    let loadHtml options (tc: TemplateContext) (so: ScriptObject) =
        tc.TemplateLoader <- htmlTemplateLoader
        let functions = HtmlFunctions options
        so.Import functions
        so.Import typeof<HtmlFunctions>

    let loadGrammar g so =
        let functions = GrammarFunctions g
        functions.LoadInstanceMethods so
        so.Import functions
        so.Import typeof<GrammarFunctions>

    let loadConflictReport grammarDef errors so =
        let functions = ConflictReportFunctions(grammarDef, errors)
        functions.LoadInstanceMethods so
        so.Import functions
        so.Import typeof<ConflictReportFunctions>

    let createDefaultScriptObject() =
        let so = ScriptObject()
        so.SetValue("farkle", defaultFarkleObject, true)
        so
