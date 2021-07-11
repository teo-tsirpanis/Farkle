// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.ProjectResolver

open System
open System.IO
open Microsoft.Build.Evaluation
open Microsoft.Build.Locator
open Serilog

type ProjectResolverOptions = {
    Configuration: string
}

let cannotFindMSBuildMessage =
    "Could not find a compatible MSBuild installation. Make sure that the .NET Core SDK is installed."

let registerMSBuild() =
    try
        let instance = MSBuildLocator.RegisterDefaults()
        Log.Debug("Using MSBuild installation from {MSBuildPath}", instance.MSBuildPath)
        Ok()
    with
    | :? InvalidOperationException ->
        Log.Error(cannotFindMSBuildMessage)
        Error()

let preferDll x =
    Log.Debug<_>("Project resolved to assembly {AssemblyPath}", x)
    let dllPath = Path.ChangeExtension(x, ".dll")
    if dllPath <> x && File.Exists dllPath then
        Log.Debug("Preferred the dll file in the same directory")
        dllPath
    else
        x

let resolveProjectAssembly options (projectPath: string) =
    let project = Project(projectPath, dict ["Configuration", options.Configuration], null)
    match project.GetProperty "TargetPath" with
    | null ->
        Log.Error("The project seems to not have been restored or built. Make sure that it is, \
try performing a clean build, and report a bug on GitHub if the problem persists.")
        Error()
    | prop ->
        let assemblyPath = preferDll prop.EvaluatedValue
        if File.Exists assemblyPath then
            Ok assemblyPath
        else
            Log.Error("The project's assembly at {AssemblyPath} could not be found. Make sure that the project is \
restored and built, try performing a clean build, and report a bug on GitHub if the problem persists.", assemblyPath)
            Error()
