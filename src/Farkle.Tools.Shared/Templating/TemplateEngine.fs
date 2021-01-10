// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Monads.Either
open Farkle.Tools
open Scriban
open Scriban.Runtime
open Serilog
open System.IO
open System.Reflection

module TemplateEngine =

    let private (|LanguageNames|) lang =
        match lang with
        | Language.``F#`` -> "FSharp", "F# grammar skeleton template"
        | Language.``C#`` -> "CSharp", "C# grammar skeleton template"

    let private getBuiltinTemplate key =
        let assembly = Assembly.GetExecutingAssembly()
        let resourceName = sprintf "Farkle.BuiltinTemplate.%s" key
        let resourceStream = assembly.GetManifestResourceStream(resourceName) |> Option.ofObj
        match resourceStream with
        | Some resourceStream ->
            use sr = new StreamReader(resourceStream)
            sr.ReadToEnd()
        | None -> failwithf "Cannot find resource with name '%s'." resourceName

    let private getTemplate (log: ILogger) =
        function
        | GrammarWebsite _ ->
            log.Error("Creating websites from grammars is not yet supported.")
            Error()
        | GrammarSkeleton(_, LanguageNames(langName, templateName), _) ->
            let resourceKey = sprintf "Grammar.%s" langName
            let templateText = getBuiltinTemplate resourceKey
            parseScribanTemplate log templateText templateName
        | GrammarCustomTemplate(_, path, _) -> either {
            let! path = assertFileExistsEx log path
            let templateText = File.ReadAllText path
            return! parseScribanTemplate log templateText path
            }
        | LALRConflictReport ->
            log.Error("Creating LALR conflict reports is not yet supported.")
            Error()

    let private createTemplateContext templateType =
        let tc = TemplateContext()
        tc.StrictVariables <- true

        let so = Utilities.createDefaultScriptObject()
        match templateType with
        | GrammarSkeleton(g, _, ns) ->
            Utilities.loadGrammar g so
            let ns =
                ns
                |> Option.defaultValue (Path.GetFileNameWithoutExtension g.GrammarPath)
            so.SetValue("namespace", ns, true)
        | GrammarCustomTemplate(g, _, options) ->
            Utilities.loadGrammar g so
            let properties = ScriptObject()
            for propKey, propValue in options.AdditionalProperties do
                so.SetValue(propKey, propValue, true)
            so.SetValue("properties", properties, true)
        | GrammarWebsite _ | LALRConflictReport ->
            raise (System.NotImplementedException())
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
