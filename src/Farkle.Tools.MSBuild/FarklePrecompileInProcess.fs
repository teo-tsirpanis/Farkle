// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Grammars
open Farkle.Tools.Precompiler
open Farkle.Tools.Precompiler.Weaver
open Farkle.Tools.Templating
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Sigourney
open System
open System.IO
open System.Threading

/// An MSBuild task that precompiles the grammars
/// of an assembly from the same MSBuild process.
/// Can only run from .NET Core editions of MSBuild.
type FarklePrecompileInProcess() as this =
    inherit MSBuildWeaver()
    do this.WeaverName <- typeof<PrecompilerWeaver>.Assembly.GetName().Name

    let mutable precompiledGrammars = null

    let cts = new CancellationTokenSource()

    static let tryParseErrorMode (x: string) =
        match Enum.TryParse<ConflictReportMode>(x, true) with
        | true, errorMode ->
            if Enum.IsDefined errorMode then
                errorMode
                |> ValueSome
            else
                ValueNone
        | _ -> ValueNone

    let precompileAssemblyFromPath fCreateConflictReport errorMode assemblyPath =
        let options = PrecompilerOptions()
        options.CancellationToken <- cts.Token
        options.ConflictReportMode <- errorMode
        options.Logger <- this.Log
        options.add_OnGrammarConflict <| Action<_> fCreateConflictReport

        this.Log.LogMessage(MessageImportance.Low, "References:")
        this.AssemblyReferences
        |> Seq.filter (fun x -> not x.IsReferenceAssembly)
        |> Seq.iter (fun x ->
            this.Log.LogMessage(MessageImportance.Low, "{0}: '{1}'", x.AssemblyName.FullName, x.FileName)
            options.AssemblyReferences.Add(x.AssemblyName.FullName, x.FileName))

        PrecompilerHost.PrecompileAssemblyFromPath(assemblyPath, options)

    member val SkipConflictReport = false with get, set

    member val ErrorMode = "" with get, set

    [<Output>]
    member val GeneratedConflictReports = Array.Empty() with get, set

    override this.Execute() =
        try
            let generatedConflictReports = ResizeArray()
            let conflictReportOutDir = Path.GetDirectoryName this.AssemblyPath
            let errorMode =
                let fromSkipConflictReport =
                    if this.SkipConflictReport then ConflictReportMode.ErrorsOnly else ConflictReportMode.ReportOnly
                if String.IsNullOrWhiteSpace this.ErrorMode then
                    fromSkipConflictReport
                else
                    match tryParseErrorMode this.ErrorMode with
                    | ValueSome x -> x
                    | ValueNone ->
                        // TODO: Localize message
                        this.Log.LogWarning "Could not recognize the value of FarklePrecompilerErrorMode, defaulting to ReportOnly."
                        ConflictReportMode.ReportOnly

            let fCreateConflictReport =
                Grammar.ofBytes >>
                TemplateEngine.createConflictReport
                    generatedConflictReports this.Log2 conflictReportOutDir
            let grammars =
                precompileAssemblyFromPath fCreateConflictReport errorMode this.AssemblyPath

            this.GeneratedConflictReports <-
                generatedConflictReports
                |> Seq.map (fun x -> TaskItem x :> ITaskItem)
                |> Array.ofSeq

            if this.GeneratedConflictReports.Length <> 0 then
                // TODO: Localize message
                this.Log.LogMessage(MessageImportance.High, "Instead of creating an HTML report, the individual conflicts \
can be shown as errors by setting the 'FarklePrecompilerErrorMode' MSbuild property to 'Both' or 'ErrorsOnly'.")

            precompiledGrammars <- grammars

            not cts.IsCancellationRequested
            && not this.Log.HasLoggedErrors
            // With our preparation completed, Sigourney will eventually call DoWeave.
            && base.Execute()
        with
        | :? OperationCanceledException as oce when oce.CancellationToken = cts.Token -> false
    override _.DoWeave asm =
        PrecompilerWeaver.Weave(asm.MainModule, this.AssemblyReferences, precompiledGrammars)
        precompiledGrammars.Count > 0
    interface ICancelableTask with
        member _.Cancel() = cts.Cancel()
