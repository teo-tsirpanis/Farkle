// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle
open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.Tools
open System.Collections.Generic
open Scriban
open Scriban.Runtime
open Serilog
open System
open System.IO

type Symbols = {
    Terminals: Terminal[]
    Nonterminals: Nonterminal[]
    NoiseSymbols: Noise[]
}

type Grammar = {
    Properties: Dictionary<string,string>
    Symbols: Symbols
    Productions: Production[]
}
with
    [<ScriptMemberIgnore>]
    static member Create (g: Grammar.Grammar) =
        let conv = Array.ofSeq
        let properties = Dictionary(g.Properties, StringComparer.OrdinalIgnoreCase)
        {
            Properties = properties
            Symbols = {
                Terminals = conv g.Symbols.Terminals
                Nonterminals = conv g.Symbols.Nonterminals
                NoiseSymbols = conv g.Symbols.NoiseSymbols
            }
            Productions = conv g.Productions
        }

type FarkleObject = {
    Version: string
}
with
    static member Create = {Version = toolsVersion}

type FarkleRoot = {
    Farkle: FarkleObject
    Grammar: Grammar
    [<ScriptMemberIgnore>]
    GrammarBytes: byte[]
    GrammarFile: string
}
with
    [<ScriptMemberIgnore>]
    static member Create grammar grammarFile bytes = {
        Farkle = FarkleObject.Create
        Grammar = grammar
        GrammarBytes = bytes
        GrammarFile = grammarFile
    }

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}

module TemplateEngine =
    let renderTemplate (log: ILogger) additionalProperties grammarFile templateSource = either {
        let templateText, templateFileName = BuiltinTemplates.getLanguageTemplate templateSource
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
        additionalProperties |> List.iter (fun (key, value) -> grammar.Properties.[key] <- value)
        let fr = FarkleRoot.Create grammar grammarFile bytes

        let so = ScriptObject()
        so.Import fr
        so.Import("to_base_64", Func<_,_>(Utilities.toBase64 fr.GrammarBytes))
        Utilities.load so

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
