// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Grammar
open System
open System.IO
open Serilog

/// A special kind of file path that also specifies the name of a precompiled grammar.
/// The format is `filePath::grammarName`. The double colons and the second part can be
/// omitted if the file has only one precompiled grammar. The file in the first path can
/// be either an assembly or a project file. If it is ommitted, a suitable project file
/// will be searched in the current directory.
type CompositePath =
    | DefaultPath
    | FileOnly of string
    | GrammarOnly of string
    | FullPath of filePath: string * grammarName: string
    static member Separator = "::"

module CompositePath =

    let create path =
        let sep = CompositePath.Separator
        match path with
        | None -> DefaultPath
        | Some path when
            String.IsNullOrWhiteSpace(path)
            || path.AsSpan().Trim().Equals(sep.AsSpan(), StringComparison.Ordinal) -> DefaultPath
        | Some path ->
            match path.IndexOf(sep) with
            | -1 -> FileOnly path
            | 0 ->
                let grammarName = path.Substring(sep.Length)
                GrammarOnly grammarName
            | doubleColonPos ->
                let filePath = path.Substring(0, doubleColonPos)
                let grammarName = path.Substring(doubleColonPos + sep.Length)
                FullPath(filePath, grammarName)

    let private resolveGrammar grammarName (filePath: string) =
        let ext = Path.GetExtension(filePath.AsSpan())
        if isProjectExtension ext then
            Log.Error("Opening projects is not currently supported")
            Error()
        elif isAssemblyExtension ext then
            use loader = new PrecompiledAssemblyFileLoader(filePath)
            match grammarName with
            | None ->
                match loader.Grammars.Count with
                | 0 ->
                    Log.Error("The assembly of {Path} has no precompiled grammars.", filePath)
                    Error()
                | 1 ->
                    loader.Grammars.Values
                    |> Seq.exactlyOne
                    |> (fun x -> x.GetGrammar() |> Ok)
                | _ ->
                    Log.Error("The assembly of {Path} has more than one precompiled gramamr:", filePath)
                    for x in loader.Grammars.Keys do
                        Log.Information("{GrammarName}", x)

                    Log.Information("You can explicitly choose the precompiled grammar you \
    want by appending {CompositePathSuffixHint} to the input file.", "'::<grammar-name>'")
                    Error()
            | Some grammarName ->
                match loader.Grammars.TryGetValue(grammarName) with
                | true, grammar -> grammar.GetGrammar() |> Ok
                | false, _ ->
                    Log.Error("The assembly of {Path} doesn't have a precompiled grammar named {GrammarName}.", grammarName)

                    Log.Information("Hint: Run {CommandHint} to list all precompiled grammars of a project's assembly.", "farkle list")
                    Error()
        elif isGrammarExtension ext then
            EGT.ofFile filePath |> Ok
        else
            Log.Error("Unsupported file name: {FilePath}", filePath)
            Error()

    let private findDefaultProject currentDir =
        Directory.EnumerateFiles(currentDir, "*.??proj", SearchOption.TopDirectoryOnly)
        |> Seq.filter(fun path -> isProjectExtension(Path.GetExtension(path.AsSpan())))
        |> List.ofSeq
        |> function
        | [] ->
            Log.Error("No project file was found in the current directory.")
            Error()
        | [x] ->
            Log.Debug("Found project file: {ProjectFile}", x)
            Ok x
        | _ ->
            Log.Error("Many project files were found in the current directory.")
            Error()

    let rec resolveEx currentDir compositePath =
        match compositePath with
        | DefaultPath ->
            match findDefaultProject currentDir with
            | Ok projectFile -> resolveEx currentDir (FileOnly projectFile)
            | Error() -> Error()
        | FileOnly filePath -> resolveGrammar None filePath
        | GrammarOnly _ -> resolveEx currentDir DefaultPath
        | FullPath(filePath, grammarName) -> resolveGrammar (Some grammarName) filePath

    let resolve compositePath = resolveEx Environment.CurrentDirectory compositePath
