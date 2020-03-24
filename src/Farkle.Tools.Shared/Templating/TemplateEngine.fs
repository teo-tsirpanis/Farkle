// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle
open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.Tools
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
        let grammar = EGT.ofFile grammarFile |> Grammar.Create
        log.Verbose("{grammarFile} was read successfully", grammarFile)
        let ns = generatedFileNamespace |> Option.defaultValue (Path.GetFileNameWithoutExtension(grammarFile))
        let fr = FarkleRoot.Create grammar grammarFile ns bytes

        let so = ScriptObject()
        so.Import fr
        so.Import("to_base_64", Func<_,_>(Utilities.toBase64 fr.GrammarBytes))
        Utilities.load fr.Grammar so
        tc.PushGlobal so

        let! template = parseScribanTemplate log templateText templateFileName

        log.Verbose("Rendering template")
        let output = template.Render(tc)
        return {
            FileExtension =
                match so.TryGetValue("file_extension") with
                    | true, fExt -> fExt.ToString()
                    | false, _ -> "out"
            Content = output}
    }
