// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Monads.Either
open Farkle.Tools
open Scriban
open Scriban.Parsing
open Scriban.Runtime
open Serilog
open System.IO

module TemplateEngine =

    let private parseTemplate (log: ILogger) templateText templateFileName =
        log.Debug("Parsing {TemplateFileName}", templateFileName.ToString())
        let template = Template.Parse(templateText, templateFileName)
        for x in template.Messages do
            match x.Type with
            | ParserMessageType.Error -> log.Error("{Error:l}", x)
            | ParserMessageType.Warning -> log.Warning("{Warning:l}", x)
            | _ -> ()
        if template.HasErrors then
            log.Error("Parsing {TemplateFileName} failed.", templateFileName)
            Error()
        else
            Ok template

    let private getTemplate (log: ILogger) =
        function
        | GrammarHtml _ ->
            let templateText = ResourceLoader.load "Html.Root.scriban"
            let templateName = "HTML root template"
            parseTemplate log templateText templateName
        | GrammarCustomTemplate(_, path, _) ->
            let templateText = File.ReadAllText path
            parseTemplate log templateText path

    let private createTemplateContext templateType =
        let tc = TemplateContext()
        tc.StrictVariables <- true
        tc.LoopLimit <- 0
        tc.LimitToString <- 0

        let so = Utilities.createDefaultScriptObject()
        match templateType with
        | GrammarHtml(g, options) ->
            Utilities.loadGrammar g so
            Utilities.loadHtml options tc so
        | GrammarCustomTemplate(g, _, options) ->
            Utilities.loadGrammar g so
            let properties = ScriptObject()
            for propKey, propValue in options.AdditionalProperties do
                so.SetValue(propKey, propValue, true)
            so.SetValue("properties", properties, true)
        tc.PushGlobal so
        tc

    let renderTemplate log templateType = either {
        let! template = getTemplate log templateType
        let tc = createTemplateContext templateType

        log.Verbose("Rendering template")
        let output = template.Render(tc)
        let fileExtension =
            match tc.CurrentGlobal.TryGetValue "file_extension" with
            | true, x -> x.ToString()
            | false, _ -> ".out.txt"
        return {
            FileExtension = fileExtension
            Content = output
        }
    }

    let createConflictReport log outputDir grammar =
        let templateInput = {Grammar = grammar; GrammarPath = ""}
        let templateType = GrammarHtml(templateInput, HtmlOptions.Default)
        match renderTemplate log templateType with
        | Ok gt ->
            let fileName = sanitizeUnsafeFileName log grammar.GrammarInfo.Name + gt.FileExtension
            let path = sprintf "%s%c%s" outputDir Path.DirectorySeparatorChar fileName
            File.WriteAllText(path, gt.Content)
            ValueSome path
        | _ ->
            log.Error("Internal error: failed to render the conflict report. Please open a GitHub issue.")
            ValueNone
