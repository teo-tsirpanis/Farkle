// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
module Farkle.Tools.Common

open Scriban
open Scriban.Parsing
open Serilog
open System.IO
open System.Reflection

/// The version of the currently executing assembly.
let toolsVersion =
    let asm = Assembly.GetExecutingAssembly()
    asm.GetCustomAttributes<AssemblyInformationalVersionAttribute>()
    |> Seq.map(fun x -> x.InformationalVersion)
    |> Seq.tryExactlyOne
    |> Option.defaultWith (fun () -> asm.GetName().Version.ToString())

/// Logs an error if the given filename does not exist.
let assertFileExists fileName =
    let fileName = Path.GetFullPath fileName
    if File.Exists fileName then
        Ok fileName
    else
        Log.Error("File {fileName} does not exist.", fileName)
        Error()

let parseScribanTemplate (log: ILogger) templateText templateFileName =
    let template = Template.Parse(templateText, templateFileName)
    template.Messages.ForEach(fun x ->
        match x.Type with
        | ParserMessageType.Error -> log.Error("{Error}", x)
        | ParserMessageType.Warning -> log.Warning("{Warning}", x)
        | _ -> ())
    if template.HasErrors then
        log.Error("Parsing {TemplateFileName} failed.", templateFileName)
        Error()
    else
        Ok template
