// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Templating.CreateTemplate

open Argu
open Farkle
open Farkle.Tools.Templating.BuiltinTemplates
open Scriban
open System.IO

type TemplateArguments =
    | [<MainCommand; ExactlyOnce; Last>] GrammarFile of string
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique>] Type of TemplateType
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "The *.egt grammar file to parse. Required."
            | Language _ -> "Specifies the language of the template to create. Defaults to F#."
            | Type _ -> "Specifies the type of the template to create, i.e. either a disposable file containing the grammar's types, or a skeleton for a post-processor. Defaults to the former."
            | TemplateFile _ -> "Specifies the template file to use, in case you want a custom one."
            | OutputFile _ -> "Specifies where the generated output will be stored. Defaults to the template's name, with the extension as set by the template, which defaults to 'out'."

let getFileContentsAndName fileName =
    File.ReadAllText fileName, fileName

let doTemplate (args: ParseResults<_>) =
    let grammarFile = args.PostProcessResult(<@ GrammarFile @>, getFileContentsAndName >> fst)
    let typ = args.GetResult(<@ Type @>, defaultValue = TemplateType.Grammar)
    let language = args.GetResult(<@ Language @>, defaultValue = Language.``F#``)
    let templateText, templateFileName =
        args.TryPostProcessResult(<@ TemplateFile @>, getFileContentsAndName)
        |> Option.defaultWith (fun () -> getLanguageTemplate typ language)

    let tc, fGetFileExtension = TemplateEngine.createTemplateContext grammarFile |> returnOrFail

    let template = Template.Parse(templateText, templateFileName)
    let output = template.Render(tc)

    let outputFile = args.TryGetResult <@ OutputFile @> |> Option.defaultWith (fun () -> Path.ChangeExtension(grammarFile, fGetFileExtension()))

    // TODO: Add proper logging support
    File.WriteAllText(outputFile, output)
