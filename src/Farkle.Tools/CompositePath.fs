// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.Tools.Templating
open System
open System.IO
open Serilog

/// A special kind of file path that also specifies the name of a precompiled grammar.
/// The format is `filePath::grammarName`. The double colons and the second part can be
/// omitted if the file has only one precompiled grammar. The file in the first path can
/// be either an assembly or a project file. If it is ommitted, a suitable project file
/// will be searched in the current directory.
type CompositePath = CompositePath of filePath: string option * grammarName: string option
with
    static member Separator = "::"

module CompositePath =

    let private defaultCompositePath = CompositePath(None, None)

    let private checkForWhitespace (x: string) =
        if String.IsNullOrWhiteSpace(x) then
            None
        else
            Some x

    let create path =
        let sep = CompositePath.Separator
        match path with
        | None -> defaultCompositePath
        | Some path when
            String.IsNullOrWhiteSpace(path)
            || path.AsSpan().Trim().Equals(sep.AsSpan(), StringComparison.Ordinal) -> defaultCompositePath
        | Some path ->
            match path.IndexOf(sep) with
            | -1 -> CompositePath(Some path, None)
            | doubleColonPos ->
                let filePath =
                    path.Substring(0, doubleColonPos)
                    |> checkForWhitespace
                let grammarName =
                    path.Substring(doubleColonPos + sep.Length)
                    |> Some
                CompositePath(filePath, grammarName)

    let private resolveGrammar grammarName (filePath: string) = either {
        let! _ = assertFileExists filePath
        let ext = Path.GetExtension(filePath.AsSpan())
        if isProjectExtension ext then
            Log.Error("Opening projects is not currently supported")
            return! Error()
        elif isAssemblyExtension ext then
            use loader = new PrecompiledAssemblyFileLoader(filePath)
            match grammarName with
            | None ->
                match loader.Grammars.Count with
                | 0 ->
                    Log.Error("The assembly of {Path} has no precompiled grammars.", filePath)
                    return! Error()
                | 1 ->
                    return GrammarTemplateInput.Create ((Seq.exactlyOne loader.Grammars.Values).GetGrammar()) filePath
                | _ ->
                    Log.Error("The assembly of {Path} has more than one precompiled gramamr:", filePath)
                    for x in loader.Grammars.Keys do
                        Log.Information("{GrammarName}", x)

                    Log.Information("You can explicitly choose the precompiled grammar you \
    want by appending {CompositePathSuffixHint} to the input file.", "'::<grammar-name>'")
                    return! Error()
            | Some grammarName ->
                match loader.Grammars.TryGetValue(grammarName) with
                | true, grammar -> return GrammarTemplateInput.Create (grammar.GetGrammar()) filePath
                | false, _ ->
                    Log.Error("The assembly of {Path} doesn't have a precompiled grammar named {GrammarName}.", grammarName)

                    Log.Information("Hint: Run {CommandHint} to list all precompiled grammars of a project's assembly.", "farkle list")
                    return! Error()
        elif isGrammarExtension ext then
            return GrammarTemplateInput.Create (EGT.ofFile filePath) filePath
        else
            Log.Error("Unsupported file name: {FilePath}", filePath)
            return! Error()
    }

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

    let rec resolve currentDir (CompositePath(filePath, grammarName)) = either {
        let! filePath =
            match filePath with
            | Some x -> Ok x
            | None -> findDefaultProject currentDir
        return! resolveGrammar grammarName filePath
    }
