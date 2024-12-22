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
open Fake.Tools
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

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Farkle"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "A modern and easy-to-use parser library for F#"

// List of author names (for NuGet package)
let authors = [ "Theodore Tsirpanis" ]

// File system information
let solutionFile  = "./Farkle.sln"

// Default target configuration
let configuration = DotNet.BuildConfiguration.Release

// Configuration when building documentation
let documentationConfiguration = DotNet.BuildConfiguration.Debug
let configurationAsString = sprintf "%A" configuration

let sourceFilesToGenerate = [
    "./src/ProductionBuilders.scriban", "./src/Farkle/Builder/ProductionBuilders.g.cs"
]

let DocumentationAssemblyFramework = "netstandard2.0"

let farkleProject = "./src/Farkle/Farkle.csproj"

let farkleToolsProject = "./src/Farkle.Tools/Farkle.Tools.fsproj"

let farkleToolsMSBuildProject = "./src/Farkle.Tools.MSBuild/Farkle.Tools.MSBuild.fsproj"

let sourceProjects = [
    farkleProject
    farkleToolsProject
    farkleToolsMSBuildProject
]

// The project to be tested
let testProject = "./tests/Farkle.Tests.CSharp/Farkle.Tests.CSharp.csproj"

let fsharpTestProjects = [
    "./tests/Farkle.Tests/Farkle.Tests.fsproj"
    "./tests/Farkle.Tools.Shared.Tests/Farkle.Tools.Shared.Tests.fsproj"
]

let msBuildTestProject = "./tests/Farkle.Tools.MSBuild.Tests/Farkle.Tools.MSBuild.Tests.csproj"

let localPackagesFolder = "./tests/packages/"

let projects = !! "**/*.??proj" -- "**/*.shproj"

// The project to be benchmarked
let benchmarkProject = "./performance/Farkle.Benchmarks/Farkle.Benchmarks.csproj"

// Additional command line arguments passed to BenchmarkDotNet.
let benchmarkArguments = Environment.environVarOrDefault "FARKLE_BENCHMARK_ARGS" "-f * --memory true -e github json"

let benchmarkReports = !! (Path.getDirectory benchmarkProject @@ "BenchmarkDotNet.Artifacts/results/*-report-github.md")

let packOutputDirectory = "./bin/"

let nugetPackages = !! "./bin/*.nupkg"

let releaseArtifacts = nugetPackages

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "teo-tsirpanis"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = "Farkle"

// Read additional information from the release notes document
let releaseInfo = lazy (ReleaseNotes.load "./RELEASE_NOTES.md")

let lastCommitMessage = lazy (Git.CommitMessage.getCommitMessage Environment.CurrentDirectory)

let releaseNotes() =
    let lines s = seq {
        use sr = new StringReader(if isNull s then "" else s)
        let mutable s = ""
        s <- sr.ReadLine()
        while not <| isNull s do
            yield s
            s <- sr.ReadLine()
    }
    match BuildServer.buildServer with
    | GitHubActions ->
        sprintf "This is a build from the commit with id: %s from branch %s/%s"
            GitHubActions.Environment.Sha
            GitHubActions.Environment.Repository
            GitHubActions.Environment.Ref
        :: (lastCommitMessage.Value |> lines |> List.ofSeq)
    | _ -> releaseInfo.Value.Notes

let nugetVersion =
    let nugetVersion = releaseInfo.Value.NugetVersion
    match BuildServer.buildServer with
    | GitHubActions -> sprintf "%s-ci.%s+%s" nugetVersion GitHubActions.Environment.RunNumber GitHubActions.Environment.Sha
    | _ -> nugetVersion

Target.description "Checks whether the release notes entry has a date"
Target.create "CheckForReleaseNotesDate" (fun _ ->
    let releaseInfo = releaseInfo.Value
    if releaseInfo.Date.IsNone then
        failwithf "The release notes entry for version %s does not have a date" releaseInfo.NugetVersion
)

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

let pushArtifact x = Trace.publish (ImportData.BuildArtifactWithName <| Path.getFullName x) x

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
    sourceProjects
    |> Seq.iter (
        DotNet.pack (fun p ->
            {p with
                Configuration = configuration
                MSBuildParams =
                    {p.MSBuildParams with
                        Properties = ("ContinuousIntegrationBuild", "true") :: p.MSBuildParams.Properties
                    }
                OutputPath = Some packOutputDirectory
            }
            |> fCommonOptions
        )
    )
    Seq.iter pushArtifact nugetPackages
)

// --------------------------------------------------------------------------------------
// Generate the documentation

let docsOutput = Path.GetFullPath "_site/"
let farkle6Repo = "temp/farkle6"
let farkle6DocsProject = farkle6Repo @@ farkleProject

Target.description "Cleans the output documentation directory"
Target.create "CleanDocs" (fun _ ->
    Shell.cleanDir docsOutput
)

let generateDocs doWatch isRelease =
    let arguments = [
        if doWatch then "watch" else "build"
        "--clean"
        "--projects"
        Path.GetFullPath farkle6DocsProject
        "--output"
        docsOutput
        "--strict"
        "--properties"
        $"TargetFramework={DocumentationAssemblyFramework}"
        if not isRelease then
            "--parameters"
            "root"
            "file://" + docsOutput.Replace("\\", "/")
    ]

    CreateProcess.fromRawCommand "fsdocs" arguments
    |> CreateProcess.withToolType (ToolType.CreateLocalTool())
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

Target.description "Prepares the reference documentation generator"
Target.create "PrepareDocsGeneration" (fun _ ->
    Git.Repository.cloneSingleBranch "." "https://github.com/teo-tsirpanis/Farkle.git" "release/6.0" farkle6Repo
    DotNet.build (fun p ->
        {p with
            Configuration = documentationConfiguration
            Framework = Some DocumentationAssemblyFramework
            MSBuildParams =
                {p.MSBuildParams with
                    // The 6.x branch does not use central package management and because
                    // it is cloned inside the new code, it inherits it. We disable it.
                    Properties = ("ManagePackageVersionsCentrally", "false") :: p.MSBuildParams.Properties
                }
        }
    ) farkle6DocsProject
)

Target.description "Watches the documentation source folder and regenerates it on every file change"
Target.create "KeepGeneratingDocs" (fun _ ->
    generateDocs true false
)

Target.description "Generates the website for the project - for release"
Target.create "GenerateDocs" (fun _ ->
    generateDocs false true
)

Target.description "Generates the website for the project - for local use"
Target.create "GenerateDocsDebug" (fun _ ->
    generateDocs false false
)

let (==>!) x y = x ==> y |> ignore
let (=?>!) x y = x =?> y |> ignore
let (?=>!) x y = x ?=> y |> ignore

"Clean"
    ==>! "GenerateCode"

["PrepareMSBuildTests"; "NuGetPack"; "Benchmark"; "PrepareDocsGeneration"]
|> List.iter (fun target -> "GenerateCode" ==>! target)

["RunMSBuildTestsNetCore"; "RunMSBuildTestsNetFramework"]
|> List.iter (fun target -> "PrepareMSBuildTests" ==>! target)

"TestLegacy" <== ["RunMSBuildTestsNetCore"]

"RunMSBuildTestsNetFramework"
    =?>! ("TestLegacy", OperatingSystem.IsWindows())

"Test" <== ["RunCSharpUnitTests"; "RunFSharpUnitTests"]

// We used to have "Test" ==>! "NuGetPack".
// This dependency will be expressed higher at the GitHub Actions level.

[""; "Debug"]
|> List.iter (fun x ->
    "CleanDocs"
        ==> "PrepareDocsGeneration"
        ==>! (sprintf "GenerateDocs%s" x))

"PrepareDocsGeneration"
    ==>! "KeepGeneratingDocs"

"Clean"
    ==>! "NuGetPack"

// --------------------------------------------------------------------------------------
// Run NuGetPack by default. Invoke './build.ps1 -t <Target>' to override

Target.runOrDefaultWithArguments "NuGetPack"
