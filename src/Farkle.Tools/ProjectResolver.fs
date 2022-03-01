// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.ProjectResolver

open System
open System.Collections.Generic
open System.IO
open Microsoft.Build.Evaluation
open Microsoft.Build.Locator
open Serilog

type ProjectResolverOptions = {
    Configuration: string option
    TargetFramework: string option
}

let cannotFindMSBuildMessage =
    "Could not find a compatible MSBuild installation. Make sure that the .NET Core SDK is installed."

let registerMSBuild() =
    try
        let instance = MSBuildLocator.RegisterDefaults()
        Log.Debug("Using MSBuild installation from {MSBuildPath:l}.", instance.MSBuildPath)
        Ok()
    with
    | :? InvalidOperationException ->
        Log.Error(cannotFindMSBuildMessage)
        Error()

let private preferDll x =
    Log.Debug<_>("Project resolved to assembly {AssemblyPath:l}.", x)
    let dllPath = Path.ChangeExtension(x, ".dll")
    if dllPath <> x && File.Exists dllPath then
        Log.Debug("Preferred the dll file in the same directory.")
        dllPath
    else
        x

let private getGlobalProperties options =
    let dict = Dictionary()
    match options.Configuration with
    | Some x -> dict.["Configuration"] <- x
    | None -> ()
    match options.TargetFramework with
    | Some x -> dict.["TargetFramework"] <- x
    | None -> ()
    dict :> IDictionary<_,_>

let private hasProperty (project: Project) propName =
    project.GetProperty propName
    |> isNull
    |> not

let resolveProjectAssembly options (projectPath: string) =
    let project = Project(projectPath, getGlobalProperties options, null)
    match project.GetProperty "TargetPath" with
    | null ->
        if options.TargetFramework.IsNone && hasProperty project "TargetFrameworks" then
            Log.Error("The project targets multiple frameworks. Please select one using the \
{LongOption} or {ShortOption} command-line options.", "--framework", "-f")
        else
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
