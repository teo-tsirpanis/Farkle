// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open Farkle.Tools.Precompiler
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
    do this.WeaverName <- PrecompilerCommon.weaverName

    let mutable precompiledGrammars = []

    let cts = new CancellationTokenSource()

    member val SkipConflictReport = false with get, set

    [<Output>]
    member val GeneratedConflictReports = Array.Empty() with get, set

    override this.Execute() =
        try
            let generatedConflictReports = ResizeArray()
            let conflictReportOutDir = Path.GetDirectoryName this.AssemblyPath
            let fCreateConflictReport =
                TemplateEngine.createConflictReport
                    this.SkipConflictReport generatedConflictReports this.Log2 conflictReportOutDir
            let grammars =
                PrecompilerInProcess.precompileAssemblyFromPath
                    cts.Token this.Log2 fCreateConflictReport this.AssemblyReferences this.AssemblyPath

            this.GeneratedConflictReports <-
                generatedConflictReports
                |> Seq.map (fun x -> TaskItem x :> ITaskItem)
                |> Array.ofSeq

            if this.GeneratedConflictReports.Length <> 0 then
                this.Log2.Information(PrecompilerCommon.conflictReportHint)

            match grammars with
            | Ok grammars ->
                precompiledGrammars <- grammars

                not cts.IsCancellationRequested
                && not this.Log.HasLoggedErrors
                // With our preparation completed, Sigourney will eventually call DoWeave.
                && base.Execute()
            | Error () -> false
        with
        | :? OperationCanceledException as oce when oce.CancellationToken = cts.Token -> false
    override _.DoWeave asm = PrecompilerInProcess.weaveGrammars asm precompiledGrammars
    interface ICancelableTask with
        member _.Cancel() = cts.Cancel()
