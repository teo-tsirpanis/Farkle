// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Grammars
open Farkle.Monads.Either
open Farkle.Tools.Templating
open System
open System.Buffers.Binary
open System.IO
open System.Reflection.PortableExecutable
open System.Runtime.InteropServices
open Serilog

type GrammarSelector = GrammarSelector of typeName: string * key: string

/// A special kind of file path that also specifies the name of a precompiled grammar.
/// The format is `filePath(::typeName(::key)?)?`. The double colons and the second part can be
/// omitted if the file has only one precompiled grammar. The file in the first path can
/// be either an assembly or a project file. If it is ommitted, a suitable project file
/// will be searched in the current directory.
type CompositePath = CompositePath of filePath: string option * GrammarSelector option
with
    static member Separator = "::"

module CompositePath =

    let private defaultCompositePath = CompositePath(None, None)

    let private checkForWhitespace (x: ReadOnlySpan<char>) =
        if x.IsEmpty || x.IsWhiteSpace() then
            None
        else
            Some <| x.ToString()

    // Loads a file either as a Farkle grammar or as a GOLD Parser grammar, depending on its signature.
    let private loadGrammar path =
        use f = File.OpenRead(path)
        let mutable magic = 0UL
        let nRead = f.Read(MemoryMarshal.AsBytes(Span(&magic)))
        f.Position <- 0L
        if not BitConverter.IsLittleEndian then
            magic <- BinaryPrimitives.ReverseEndianness magic
        if nRead = sizeof<uint64> && magic = 0x0000656C6B726146UL then
            let bytes = f.Length |> Int32.CreateSaturating |> Array.zeroCreate
            f.ReadExactly(bytes.AsSpan())
            bytes
            |> ImmutableCollectionsMarshal.AsImmutableArray
            |> Grammar.Load
        else
            Grammar.ConvertFromGoldParser f

    let isGrammarCompatible (GrammarSelector(typeName, key)) (grammar: PrecompiledGrammar) =
        PrecompiledAssemblyFileLoader.getTypeFullName grammar = typeName
        && grammar.Key = key

    let create path =
        let sep = CompositePath.Separator
        match path with
        | None -> defaultCompositePath
        | Some path when
            String.IsNullOrWhiteSpace(path)
            || path.AsSpan().Trim().Equals(sep.AsSpan(), StringComparison.Ordinal) -> defaultCompositePath
        | Some path ->
            let ranges = Array.zeroCreate 3
            match path.AsSpan().Split(ranges.AsSpan(), CompositePath.Separator) with
            | 1 -> CompositePath(Some path, None)
            | rangeCount ->
                let filePath = checkForWhitespace(path.AsSpan(ranges[0]))
                let grammarType = path.AsSpan(ranges[1]).Trim().ToString()
                let key =
                    if rangeCount = 2 then
                        null
                    else
                        path.AsSpan(ranges[2].Start).ToString()
                CompositePath(filePath, Some <| GrammarSelector(grammarType, key))

    let rec private resolveGrammar projectOptions grammarSelector originalPath (filePath: string) = either {
        let ext = Path.GetExtension(filePath.AsSpan())
        if isProjectExtension ext then
            do! ProjectResolver.registerMSBuild()
            let! assemblyPath = ProjectResolver.resolveProjectAssembly projectOptions filePath
            if originalPath = filePath then
                // We will recurse to follow the assembly file logic.
                // Thanks to the originalPath parameter, any error will be attributed
                // to the project, not its assembly, for increased user-friendliness.
                return! resolveGrammar projectOptions grammarSelector filePath assemblyPath
            else
                // But for the very unlikely case that the project loops again, we should fail.
                Log.Fatal("An infinite loop was detected in the composite path resolver. Please report a bug on GitHub.")
                return! Error()
        elif isAssemblyExtension ext then
            use f = File.OpenRead filePath
            use pe = new PEReader(f)
            let grammars = PrecompiledAssemblyFileLoader.loadAll pe
            match grammarSelector with
            | None ->
                match grammars with
                | [] ->
                    Log.Error("The assembly of {Path} has no precompiled grammars.", originalPath)
                    return! Error()
                | [g] ->
                    return GrammarTemplateInput.Create (g.LoadGrammar()) filePath
                | _ ->
                    Log.Error("The assembly of {Path} has more than one precompiled grammar.", originalPath)
                    for x in grammars do
                        Log.Information("{GrammarName:l}", x.GetDisplayName())

                    Log.Information("You can explicitly choose the precompiled grammar you \
want by appending {CompositePathSuffixHint} to the input file.", "::<type-name>[::<key>]")
                    return! Error()
            | Some selector ->
                match Seq.tryFind (isGrammarCompatible selector) grammars with
                | Some g -> return GrammarTemplateInput.Create (g.LoadGrammar()) filePath
                | None ->
                    Log.Error("The assembly of {Path} does not contain a precompiled grammar meeting the specified criteria.", originalPath)

                    Log.Information("Hint: Run {CommandHint} to list all precompiled grammars of a project's assembly.", "farkle list")
                    return! Error()
        elif isGrammarExtension ext then
            return GrammarTemplateInput.Create (loadGrammar filePath) filePath
        else
            Log.Error("Unsupported file extension: {FileExtension}", ext.ToString())
            return! Error()
    }

    let findDefaultProject currentDir =
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

    let rec resolve projectOptions currentDir (CompositePath(filePath, grammarName)) = either {
        let! filePath =
            match filePath with
            | Some x -> Path.GetFullPath(x, currentDir) |> Ok
            | None -> findDefaultProject currentDir
        return! resolveGrammar projectOptions grammarName filePath filePath
    }
