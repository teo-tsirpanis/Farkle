// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.GeneratePredefinedSets

open Argu
open Farkle.Grammar.GOLDParser
open Farkle.Grammar.GOLDParser.EGTReader
open Farkle.Monads.Either
open Farkle.Tools
open Scriban
open Scriban.Runtime
open Serilog
open System
open System.IO
open System.Text

type Arguments =
    | [<ExactlyOnce; MainCommand>] PredefinedSetsFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | PredefinedSetsFile _ -> "The file 'sets.dat' which is bundled with the GOLD Parser Builder"
            | OutputFile _ -> "The path to write the generated F# source file with the predefined sets. \
Defaults to 'PredefinedSets.fs'."

type CharacterRange = {
    CFrom: char
    CTo: char
}

type PredefinedSet = {
    Name: string
    Category: string
    Comment: string
    Characters: CharacterRange []
}

let getCategoryFullName =
    function
    | "C" -> "Constants"
    | "M" -> "Miscellaneous"
    | "A" -> "ASCII Useful"
    | "U" -> "Unicode Useful"
    | "B" -> "Unicode Blocks"
    | x -> x

let private fHeaderCheck x =
    match x with
    | "GOLD Character Sets" -> Ok ()
    | _ -> Error InvalidEGTFile

let private correctPredefinedSet fAdd x =
    match x.Name with
    | "Digit" -> ()
    | "Euro Sign" -> fAdd {x with Category = "Constants"}
    | _ -> fAdd x

let private fRecord (fAdd: _ -> unit) (mem: ReadOnlyMemory<_>) =
    lengthMustBeAtLeast mem 4
    let name = wantString mem 0
    let category = wantString mem 1 |> getCategoryFullName
    let comment = wantString mem 2
    Log.Debug("Read character set with name {Name}, category {Category} and comment {Comment}", name, category, comment)
    let count = wantUInt16 mem 3 |> int
    lengthMustBe mem <| 4 + 2 * count
    let mem = mem.Slice(4)
    let characters = Array.zeroCreate count
    for i = 0 to count - 1 do
        let c1 = 2 * i + 0 |> wantUInt16 mem |> char
        let c2 = 2 * i + 1 |> wantUInt16 mem |> char
        Array.set characters i {CFrom = c1; CTo = c2}
    {Name = name; Category = category; Comment = comment; Characters = characters} |> fAdd

let private readPredefinedSets filePath =
    use stream = File.OpenRead(filePath)
    use br = new BinaryReader(stream)
    let list = ResizeArray()
    match readEGT fHeaderCheck (fRecord <| correctPredefinedSet list.Add) br with
    | Ok () ->
        Log.Debug("Reading {FilePath} succeeded.", filePath)
        Ok <| list.ToArray()
    | Error x ->
        Log.Error("Reading {FilePath} failed: {ErrorType}", x)
        Error()

module private Utilities =

    let hexChar (c: char) = sprintf "%04x" (int c)

    let capitalizeFirst (x: string) =
        if String.IsNullOrEmpty(x) || Char.IsUpper(x, 0) then
            x
        else
            let sb = StringBuilder()
            sb.Append(Char.ToUpperInvariant(x.[0])).Append(x, 1, x.Length - 1) |> ignore
            sb.ToString()

    let makeFSharpIndent (x: string) =
        x.Split([|' '; '-'|])
        |> Seq.map capitalizeFirst
        |> String.concat ""

    let loadUtilities(so: ScriptObject) =
        so.Import("make_fsharp_indent", Func<_,_> makeFSharpIndent)
        so.Import("hex_char", Func<_,_> hexChar)

let private template = """// This file was generated from data from the GOLD Parser Builder.

[<AutoOpen>]
/// Some common character sets that were imported from GOLD Parser.
module Farkle.Builder.PredefinedSets

{{~ for s in predefined_sets ~}}
    {{~ if s.comment != "" ~}}
    /// {{ s.comment }}
    {{~ end ~}}
    {{~ if s.characters.size == 1 ~}}
    {{~ c = s.characters[0] ~}}
    let {{ make_fsharp_indent s.name }} = Set.ofRanges ['\u{{ hex_char c.cfrom }}', '\u{{ hex_char c.cto }}']
    {{~ else ~}}
    let {{ make_fsharp_indent s.name }} = Set.ofRanges [
    {{~ for c in s.characters ~}}
        '\u{{ hex_char c.cfrom }}', '\u{{ hex_char c.cto }}'
    {{~ end ~}}
    ]
    {{~ end ~}}
{{ end }}"""

let generatePredefinedSets filePath = either {
    let! theSets = readPredefinedSets filePath
    let distinctCategories = theSets |> Seq.map (fun x -> x.Category) |> Seq.distinct |> Array.ofSeq
    Log.Debug("Template categories: {Categories}", distinctCategories)
    let! template = parseScribanTemplate Log.Logger template "Predefined Sets F# Template"
    let so = ScriptObject()
    so.SetValue("predefined_sets", theSets, true)
    Utilities.loadUtilities so

    let tc = TemplateContext()
    tc.StrictVariables <- true
    tc.PushGlobal so

    return template.Render(tc)
}

let run (args: ParseResults<_>) = either {
    let! inputFile = args.PostProcessResult(PredefinedSetsFile, assertFileExists)
    let outputFile = args.GetResult(OutputFile, "PredefinedSets.fs")

    let! generatedSource = generatePredefinedSets inputFile

    Log.Verbose("Creating file at {OutputFile}", outputFile)
    File.WriteAllText(outputFile, generatedSource)
    Log.Information("Predefined sets generated at {OutputFile}", outputFile)
}
