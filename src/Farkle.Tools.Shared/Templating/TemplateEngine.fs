// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle
open Farkle.Grammar
open Farkle.Monads.Either
open Scriban
open Scriban.Runtime
open Serilog
open System
open System.IO

module TemplateEngine =
    let renderTemplate (log: ILogger) resourceNamespace generatedFileNamespace grammarFile templateSource = either {
        let templateText, templateFileName = BuiltinTemplates.getLanguageTemplate resourceNamespace templateSource
        let tc = TemplateContext()
        tc.StrictVariables <- true
        let bytes = File.ReadAllBytes grammarFile
        let! grammar =
            match GOLDParser.EGT.ofFile grammarFile with
            | Ok grammar ->
                log.Verbose("{grammarFile} was read successfuly", grammarFile)
                grammar |> Grammar.Create |> Ok
            | Error message ->
                log.Error("Error while reading {grammarFile}: {message}", grammarFile, message)
                Error ()
        let ns = generatedFileNamespace |> Option.defaultValue (Path.GetFileNameWithoutExtension(grammarFile))
        let fr = FarkleRoot.Create grammar grammarFile ns bytes

        let so = ScriptObject()
        so.Import fr
        so.Import("to_base_64", Func<_,_>(Utilities.toBase64 fr.GrammarBytes))
        Utilities.load fr.Grammar so

        tc.PushGlobal so
        
        let template = Template.Parse(templateText, templateFileName)
        if template.HasErrors then
            template.Messages.ForEach (fun x -> Log.Error("{error}", x))
            log.Error("Parsing template {templateFileName} failed.", templateFileName)
            return! Error ()
            
        log.Verbose("Rendering template")
        let output = template.Render(tc)

        return {
            FileExtension =
                match so.TryGetValue("file_extension") with
                    | true, fExt -> fExt.ToString()
                    | false, _ -> "out"
            Content = output}
    }
