// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

open Fake.BuildServer
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Scriban
open System
open System.IO
open System.Text.RegularExpressions

Environment.GetCommandLineArgs()
|> Array.toList
// Environment.GetCommandLineArgs() contains the path to the executable as the first argument.
|> List.tail
|> Context.FakeExecutionContext.Create false "build.fs"
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

Target.initEnvironment()

BuildServer.install [ GitHubActions.Installer ]

// Default target configuration
let configuration = DotNet.BuildConfiguration.Release

let sourceFilesToGenerate = [
    "./src/ProductionBuilders.scriban", "./src/Farkle/Builder/ProductionBuilders.g.cs"
]

let farkleProject = "./src/Farkle/Farkle.csproj"

let farkleToolsProject = "./src/Farkle.Tools/Farkle.Tools.fsproj"

let farkleToolsMSBuildProject = "./src/Farkle.Tools.MSBuild/Farkle.Tools.MSBuild.fsproj"

let packProject = "./src/pack.proj"

// The project to be tested
let testProject = "./tests/Farkle.Tests.CSharp/Farkle.Tests.CSharp.csproj"

let fsharpTestProjects = [
    "./tests/Farkle.Tests/Farkle.Tests.fsproj"
    "./tests/Farkle.Tools.Shared.Tests/Farkle.Tools.Shared.Tests.fsproj"
]

let msBuildTestProject = "./tests/Farkle.Tools.MSBuild.Tests/Farkle.Tools.MSBuild.Tests.csproj"

let localPackagesFolder = "./tests/packages/"

// The project to be benchmarked
let benchmarkProject = "./performance/Farkle.Benchmarks/Farkle.Benchmarks.csproj"

// Additional command line arguments passed to BenchmarkDotNet.
let benchmarkArguments = Environment.environVarOrDefault "FARKLE_BENCHMARK_ARGS" "-f * --memory true -e github json"

let benchmarkReports = !! (Path.getDirectory benchmarkProject @@ "BenchmarkDotNet.Artifacts/results/*-report-github.md")

let packOutputDirectory = "./bin/"

let nugetPackages = !! "./bin/*.nupkg"

// Read additional information from the release notes document
let releaseInfo = lazy (ReleaseNotes.load "./RELEASE_NOTES.md")

let nugetVersion =
    let nugetVersion = releaseInfo.Value.NugetVersion
    match BuildServer.buildServer with
    | GitHubActions when GitHubActions.Environment.EventName <> "release" ->
        sprintf "%s-ci.%s+%s" nugetVersion GitHubActions.Environment.RunNumber GitHubActions.Environment.Sha
    | _ -> nugetVersion

let fReleaseConfiguration x = {x with DotNet.BuildOptions.Configuration = configuration}

let inline fCommonOptions x =
    DotNet.Options.withAdditionalArgs [
        sprintf "/p:Version=%s" nugetVersion
    ] x

let handleFailure (p: ProcessResult) =
    let exitCode = p.ExitCode
    if exitCode <> 0 then
        failwithf "Execution failed with error code %d" exitCode

let dotNetRun proj fx (config: DotNet.BuildConfiguration) buildArgs args =
    let fx = fx |> Option.map (sprintf " --framework %s") |> Option.defaultValue ""
    DotNet.exec
        (fun p -> {p with WorkingDirectory = Path.getDirectory proj})
        "run"
        (sprintf "--project %s%s -c %A %s -- %s" (Path.GetFileName proj) fx config buildArgs args)
    |> handleFailure

let cleanBinObj directory =
    directory @@ "bin" |> Shell.deleteDir
    directory @@ "obj" |> Shell.deleteDir

Target.description "Cleans the output directories"
Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target.description "Generates some required source code files"
Target.create "GenerateCode" (fun _ ->
    sourceFilesToGenerate
    |> List.iter (fun (src, dest) ->
        File.checkExists src
        let shouldGenerate =
            if File.exists dest then
                if File.GetLastWriteTimeUtc src > File.GetLastWriteTimeUtc dest then
                    Trace.logfn "Regenerating %s because it is older than %s" dest src
                    true
                else
                    Trace.logfn "Skipping %s because it is newer than %s" dest src
                    false
            else
                Trace.logfn "%s does not exist so it will be generated" dest
                true
        if shouldGenerate then
            let templateText = File.readAsString src
            let template = Template.Parse(templateText, src)
            let tc = TemplateContext()
            let generatedSource = template.Render(tc)
            File.WriteAllText(dest, generatedSource)
    )
)

Target.description "Runs the C# unit tests"
Target.create "RunCSharpUnitTests" (fun _ ->
    testProject
    |> DotNet.test id
)

Target.description "Runs the F# unit tests"
Target.create "RunFSharpUnitTests" (fun _ ->
    fsharpTestProjects
    |> List.iter (DotNet.test id)
)

Target.description "Prepares the MSBuild integration tests"
Target.create "PrepareMSBuildTests" (fun _ ->
    Shell.cleanDir localPackagesFolder
    Directory.ensure localPackagesFolder
    farkleToolsMSBuildProject
    |> DotNet.pack (fun p ->
        {p with
            OutputPath = Some localPackagesFolder
            MSBuildParams = {p.MSBuildParams with Properties = ("Version", "0.0.0-local") :: p.MSBuildParams.Properties}
        }
    )
)

Target.description "Runs the MSBuild integration tests on .NET Framework editions of MSBuild"
Target.create "RunMSBuildTestsNetFramework" (fun _ ->
    DotNet.build id farkleToolsProject

    let testProjectDirectory = Path.getDirectory msBuildTestProject
    let customWorkerPath = Path.getFullName "./src/Farkle.Tools/bin/Release/net8.0/Farkle.Tools.dll"
    // dotnet clean sometimes fails; this is faster and cleans only this project.
    cleanBinObj testProjectDirectory
    msBuildTestProject
    |> MSBuild.build (fun x ->
        {x with
            DoRestore = true
            Properties = ("FarkleCustomPrecompilerWorkerPath", customWorkerPath) :: x.Properties
            Targets = ["Build"]
            Verbosity = Some MSBuildVerbosity.Minimal
            NodeReuse = false
        }
    )

    msBuildTestProject
    |> DotNet.test (fun p ->
        {p with
            NoBuild = true
            ResultsDirectory = Some testProjectDirectory
        }
    )
)

Target.description "Runs the MSBuild integration tests on .NET Core editions of MSBuild"
Target.create "RunMSBuildTestsNetCore" (fun _ ->
    let testProjectDirectory = Path.getDirectory msBuildTestProject
    cleanBinObj testProjectDirectory
    msBuildTestProject
    |> DotNet.test (fun p ->
        {p with
            ResultsDirectory = Some testProjectDirectory
        }
    )
)

Target.description "Runs all tests of the legacy F# codebase"
Target.create "TestLegacy" ignore

Target.description "Runs all tests on the C# codebase"
Target.create "Test" ignore

Target.description "Runs all benchmarks"
Target.create "Benchmark" (fun _ ->
    dotNetRun benchmarkProject None DotNet.BuildConfiguration.Release "" benchmarkArguments
    match Environment.environVarOrNone "GITHUB_STEP_SUMMARY" with
    | Some stepSummary ->
        let regex = Regex("Farkle\.Benchmarks\.(\w+)-report-github\.md")
        use writer = new StreamWriter(File.OpenWrite stepSummary)
        writer.WriteLine("# Benchmark report")
        benchmarkReports
        |> Seq.iter(fun path ->
            let benchmarkName = regex.Match(path).Groups[1].Value
            writer.WriteLine $"## {benchmarkName}"
            File.readAsString path
            |> writer.WriteLine)
    | None -> ()
)

Target.description "Builds the NuGet packages"
Target.create "NuGetPack" (fun _ ->
    packProject
    |> DotNet.pack (fun p ->
        {p with
            Configuration = configuration
            OutputPath = Some packOutputDirectory
        }
        |> fCommonOptions
    )
)

// --------------------------------------------------------------------------------------
// Generate the documentation

let docsConfig = Path.GetFullPath "./docs/docfx.json"
let docsOutput = Path.GetFullPath "_site/"

Target.description "Cleans the output documentation directory"
Target.create "CleanDocs" (fun _ ->
    Shell.cleanDir docsOutput
)

let generateDocs doWatch =
    let arguments = [
        docsConfig
        if doWatch then "--serve"
        "--output"
        docsOutput
    ]

    CreateProcess.fromRawCommand "docfx" arguments
    |> CreateProcess.withToolType (ToolType.CreateLocalTool())
    |> if not doWatch then CreateProcess.ensureExitCode else id
    |> Proc.run
    |> ignore

Target.description "Generates the documentation for the project, and launches a local web server that hosts it"
Target.create "ServeDocs" (fun _ ->
    generateDocs true
)

Target.description "Generates the documentation for the project"
Target.create "GenerateDocs" (fun _ ->
    generateDocs false
)

let (==>!) x y = x ==> y |> ignore
let (=?>!) x y = x =?> y |> ignore
let (?=>!) x y = x ?=> y |> ignore

"Clean"
    ==>! "GenerateCode"

["PrepareMSBuildTests"; "NuGetPack"; "Benchmark"]
|> List.iter (fun target -> "GenerateCode" ==>! target)

["RunMSBuildTestsNetCore"; "RunMSBuildTestsNetFramework"]
|> List.iter (fun target -> "PrepareMSBuildTests" ==>! target)

"TestLegacy" <== ["RunMSBuildTestsNetCore"]

"RunMSBuildTestsNetFramework"
    =?>! ("TestLegacy", OperatingSystem.IsWindows())

"Test" <== ["RunCSharpUnitTests"; "RunFSharpUnitTests"]

// We used to have "Test" ==>! "NuGetPack".
// This dependency will be expressed higher at the GitHub Actions level.

"CleanDocs"
    ==>! "GenerateDocs"

"Clean"
    ==>! "NuGetPack"

// --------------------------------------------------------------------------------------
// Run NuGetPack by default. Invoke './build.ps1 -t <Target>' to override

Target.runOrDefaultWithArguments "NuGetPack"
