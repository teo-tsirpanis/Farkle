// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Monads.Either
open Farkle.Tools
open Scriban
open Scriban.Runtime
open System.IO

module TemplateEngine =
    let renderTemplate log generatedFileNamespace grammar grammarFile templateSource = either {
        let templateText, templateFileName = BuiltinTemplates.getLanguageTemplate templateSource
        let tc = TemplateContext()
        tc.StrictVariables <- true
        let ns = generatedFileNamespace |> Option.defaultValue (Path.GetFileNameWithoutExtension(grammarFile))
        let fr = FarkleRoot.Create grammar grammarFile ns

        let so = ScriptObject()
        Utilities.load fr so
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
