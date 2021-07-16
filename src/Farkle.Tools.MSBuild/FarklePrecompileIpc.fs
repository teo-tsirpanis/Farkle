// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.MSBuild

open System.IO
open System.Reflection
open Farkle.Monads.Either
open Farkle.Tools.Precompiler
open Medallion.Shell
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Sigourney
open System
open System.Linq

type FarklePrecompileIpc() =
    inherit Task()

    static let createWeaverConfig =
        // TODO: Make WeaverConfig.TryCreate public.
        typeof<WeaverConfig>
            .GetMethod("TryCreate", BindingFlags.NonPublic ||| BindingFlags.Static)
            .CreateDelegate(typeof<Func<ITaskItem[], WeaverConfig>>)
        :?> Func<ITaskItem[], WeaverConfig>

    static let createInput asmPath (config: WeaverConfig) = {
        AssemblyPath = asmPath
        References = config.References.Select(fun x -> x.FileName).ToArray()
    }

    [<Required>]
    member val AssemblyPath = "" with get, set

    [<Required>]
    member val Configuration = Array.Empty() with get, set

    member val CustomWorkerPath = "" with get, set

    member private this.RunWorkerProcess inputPath outputPath =
        let commandArgs =
            if String.IsNullOrWhiteSpace this.CustomWorkerPath then
                [|"tool"; "run"; "farkle"; "--"; "precompiler-worker"; inputPath; outputPath|]
            else
                this.Log.LogMessage(MessageImportance.Normal, "Using custom precompiler worker at '{0}'", this.CustomWorkerPath)
                [|this.CustomWorkerPath; "precompiler-worker"; inputPath; outputPath|]
        this.Log.LogMessage(MessageImportance.Normal, "Running the precompiler \
worker on input file at {0} and output file at {1}", inputPath, outputPath)

        let command = Command.Run("dotnet", unbox commandArgs)

        let commandResult = command.Result
        if commandResult.ExitCode = 0 then
            Ok()
        else
            this.Log.LogError("The precompiler worker exited with error code {0}.", commandResult.ExitCode)
            let workerStderr = commandResult.StandardError
            if not (String.IsNullOrWhiteSpace workerStderr) then
                this.Log.LogError(workerStderr.Trim())
            Error()

    member private this.ExecuteImpl() = either {
        let weaverConfig = createWeaverConfig.Invoke this.Configuration
        if isNull weaverConfig then
            this.Log.LogError("Error while getting Farkle's precompiler's configuration. Please open an issue on GitHub.")
            return! Error()
        let workerInput = createInput this.AssemblyPath weaverConfig
        let inputPath = Path.GetTempFileName()
        let outputPath = Path.ChangeExtension(inputPath, ".output.json")

        PrecompilerCommon.writeToJsonFile inputPath workerInput
        do! this.RunWorkerProcess inputPath outputPath
        let workerOutput = PrecompilerCommon.readFromJsonFile<PrecompilerWorkerOutput> outputPath

        this.Log.LogMessage(MessageImportance.Low, "Beginning precompiler worker logs")
        for ev in workerOutput.Messages do
            ev.LogTo this.BuildEngine
        this.Log.LogMessage(MessageImportance.Low, "Ending precompiler worker logs")

        return workerOutput.Success
    }

    override this.Execute() =
        match this.ExecuteImpl() with
        | Ok success -> success
        | Error () ->
            this.Log.LogMessage(MessageImportance.High, "Make sure that a matching version of the package \
Farkle.Tools is installed.")
            this.Log.LogMessage(MessageImportance.High, "Otherwise consider reporting the problem on GitHub. \
In the meantime, try building your project with the .NET SDK.")
            false
