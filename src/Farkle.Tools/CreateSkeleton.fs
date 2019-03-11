// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.CreateSkeleton

open Argu
open Farkle
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Scriban
open Scriban.Runtime
open System.IO
open System

type SkeletonArguments =
    | [<ExactlyOnce; AltCommandLine("-g")>] GrammarFile of string
    | [<ExactlyOnce; AltCommandLine("-t")>] TemplateFile of string
    | [<AltCommandLine("-o")>] OutputFile of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "specify the *.egt grammar file to parse. Required."
            | TemplateFile _ -> "specify the template file to use. Required."
            | OutputFile _ -> "specify where the generated output will be stored. Defaults to the template's name."

type FarkleSkeletonScriptObject(grammarBase64: string, grammar: Grammar) =
    inherit ScriptObject()
    let mutable fileExt = "out"

    member __.GrammarBase64 = grammarBase64
    member __.productions = grammar.Groups

    member __.file_extension(ext) = fileExt <- ext
    [<ScriptMemberIgnore>]
    member __.FileExtension = "." + fileExt

let doSkeleton (args: ParseResults<_>) =
    let grammarFile = args.GetResult <@ GrammarFile @>
    let templateFile = args.GetResult <@ TemplateFile @>
    let outputFile = args.TryGetResult <@ OutputFile @>
    eprintfn "Creating a skeleton program from %A, based on template %A, to %A..." grammarFile templateFile outputFile

    let templateText = File.ReadAllText templateFile
    let grammarBase64, grammar =
        let bytes = File.ReadAllBytes grammarFile
        use mem = new MemoryStream(bytes)
        let grammar = EGT.ofStream mem |> returnOrFail
        let grammarBase64 = Convert.ToBase64String bytes
        grammarBase64, grammar

    let template = Template.Parse(templateText, templateFile)
    let tc = TemplateContext()
    let so = FarkleSkeletonScriptObject(grammarBase64, grammar)
    tc.PushGlobal so
    let output = template.Render(tc)

    let outputFile = outputFile |> Option.defaultWith (fun () -> Path.ChangeExtension(grammarFile, so.FileExtension))
    File.WriteAllText(outputFile, output)