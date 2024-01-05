// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

open Fake.Api
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
    "./src/ProductionBuilders.scriban", "./src/Farkle/Builder/DesigntimeFarkle/ProductionBuilders.g.fs"
]

let DocumentationAssemblyFramework = "netstandard2.0"

let farkleProject = "./src/Farkle/Farkle.fsproj"

let farkleToolsProject = "./src/Farkle.Tools/Farkle.Tools.fsproj"

let farkleToolsMSBuildProject = "./src/Farkle.Tools.MSBuild/Farkle.Tools.MSBuild.fsproj"

let sourceProjects = [
    farkleProject
    farkleToolsProject
    farkleToolsMSBuildProject
]

// The project to be tested
let testProject = "./tests/Farkle.Tests.CSharp/Farkle.Tests.CSharp.csproj"

let fsharpTestProject = "./tests/Farkle.Tests/Farkle.Tests.fsproj"

let msBuildTestProject = "./tests/Farkle.Tools.MSBuild.Tests/Farkle.Tools.MSBuild.Tests.csproj"

let localPackagesFolder = "./tests/packages/"

let projects = !! "**/*.??proj" -- "**/*.shproj"

// The project to be benchmarked
let benchmarkProject = "./performance/Farkle.Benchmarks.CSharp/Farkle.Benchmarks.CSharp.csproj"

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

let getSecret name =
    let secret = Environment.environVarOrFail name
    TraceSecrets.register $"<{name}>" secret
    secret

let githubToken = lazy(getSecret "farkle-github-token")
let nugetKey = lazy(getSecret "NUGET_KEY")

let checkForReleaseCredentials _ =
    githubToken.Value |> ignore
    nugetKey.Value |> ignore

let checkForReleaseNotesDate _ =
    let releaseInfo = releaseInfo.Value
    if releaseInfo.Date.IsNone then
        failwithf "The release notes entry for version %s does not have a date" releaseInfo.NugetVersion

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

let clean _ =
    Shell.cleanDirs ["bin"; "temp"]

// --------------------------------------------------------------------------------------
// Build library & test project

let generateCode _ =
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

let runCSharpUnitTests _ =
    testProject
    |> DotNet.test id

let runFSharpUnitTests _ =
    fsharpTestProject
    |> DotNet.test id

let prepareMSBuildTests _ =
    Shell.cleanDir localPackagesFolder
    Directory.ensure localPackagesFolder
    farkleToolsMSBuildProject
    |> DotNet.pack (fun p ->
        {p with
            OutputPath = Some localPackagesFolder
            MSBuildParams = {p.MSBuildParams with Properties = ("Version", "0.0.0-local") :: p.MSBuildParams.Properties}
        }
    )

let runMSBuildTestsNetFramework _ =
    DotNet.build id farkleToolsProject

    let testProjectDirectory = Path.getDirectory msBuildTestProject
    let customWorkerPath = Path.getFullName "./src/Farkle.Tools/bin/Release/net6.0/Farkle.Tools.dll"
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

let runMSBuildTestsNetCore _ =
    let testProjectDirectory = Path.getDirectory msBuildTestProject
    cleanBinObj testProjectDirectory
    msBuildTestProject
    |> DotNet.test (fun p ->
        {p with
            ResultsDirectory = Some testProjectDirectory
        }
    )

let benchmark _ =
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

let nugetPack _ =
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

let nugetPublish _ =
    Seq.iter (DotNet.nugetPush (fun p ->
        {p with
            PushParams =
                {p.PushParams with
                    Source = Some "https://www.nuget.org"
                    ApiKey = Some nugetKey.Value
                }
        }
    )) nugetPackages

// --------------------------------------------------------------------------------------
// Generate the documentation

let docsOutput = Path.GetFullPath "_site/"
let farkle6Repo = "temp/farkle6"
let farkle6DocsProject = farkle6Repo @@ farkleProject

let moveFileTemporarily src dest =
    File.Copy(src, dest, true)
    {new IDisposable with member _.Dispose() = File.delete dest}

let cleanDocs _ =
    Shell.cleanDir docsOutput

let generateDocs doWatch isRelease =
    use __ = moveFileTemporarily "RELEASE_NOTES.md" "docs/release-notes.md"
    use __ = moveFileTemporarily "LICENSE.txt" "docs/license.md"

    let arguments = [
        if doWatch then "watch" else "build"
        "--clean"
        "--projects"
        Path.GetFullPath farkle6DocsProject
        "--output"
        docsOutput
        "--properties"
        "--strict"
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

let prepareDocsGeneration _ =
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

let keepGeneratingDocs _ =
    generateDocs true false

let generateDocsRelease _ =
    generateDocs false true

let generateDocsDebug _ =
    generateDocs false false

// --------------------------------------------------------------------------------------
// Release Scripts

let remoteToPush = lazy (
    Git.CommandHelper.getGitResult "" "remote -v"
    |> Seq.filter (String.endsWith "(push)")
    |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
    |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0])

let githubRelease _ =
    Git.Branches.tag "" nugetVersion
    Git.Branches.pushTag "" remoteToPush.Value nugetVersion

    GitHub.createClientWithToken githubToken.Value
    |> GitHub.createRelease gitOwner gitName nugetVersion
        (fun x ->
            {x with
                Name = sprintf "Version %s" nugetVersion
                Prerelease = releaseInfo.Value.SemVer.PreRelease.IsSome
                Body = releaseNotes() |> Seq.map (sprintf "* %s") |> String.concat Environment.NewLine})
    |> GitHub.uploadFiles releaseArtifacts
    |> GitHub.publishDraft
    |> Async.RunSynchronously

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build target <Target>' to override

let initTargets() =
    BuildServer.install [ GitHubActions.Installer ]

    let (==>!) x y = x ==> y |> ignore
    let (=?>!) x y = x =?> y |> ignore
    let (?=>!) x y = x ?=> y |> ignore

    Target.description "Fails the build if the appropriate environment variables for the release do not exist"
    Target.create "CheckForReleaseCredentials" checkForReleaseCredentials
    Target.description "Checks whether the release notes entry has a date"
    Target.create "CheckForReleaseNotesDate" checkForReleaseNotesDate
    Target.description "Cleans the output directories"
    Target.create "Clean" clean
    Target.description "Cleans the output documentation directory"
    Target.create "CleanDocs" cleanDocs
    Target.description "Generates some required source code files"
    Target.create "GenerateCode" generateCode
    Target.description "Prepares the MSBuild integration tests"
    Target.create "PrepareMSBuildTests" prepareMSBuildTests
    Target.description "Runs the MSBuild integration tests on .NET Framework editions of MSBuild"
    Target.create "RunMSBuildTestsNetFramework" runMSBuildTestsNetFramework
    Target.description "Runs the MSBuild integration tests on .NET Core editions of MSBuild"
    Target.create "RunMSBuildTestsNetCore" runMSBuildTestsNetCore
    Target.description "Runs all tests of the legacy F# codebase"
    Target.create "TestLegacy" ignore

    Target.description "Runs the C# unit tests"
    Target.create "RunCSharpUnitTests" runCSharpUnitTests
    Target.description "Runs the F# unit tests"
    Target.create "RunFSharpUnitTests" runFSharpUnitTests
    Target.description "Runs all tests on the C# codebase"
    Target.create "Test" ignore

    Target.description "Runs all benchmarks"
    Target.create "Benchmark" benchmark
    Target.description "Builds the NuGet packages"
    Target.create "NuGetPack" nugetPack
    Target.description "Publishes the NuGet packages"
    Target.create "NuGetPublish" nugetPublish
    Target.description "Prepares the reference documentation generator"
    Target.create "PrepareDocsGeneration" prepareDocsGeneration
    Target.description "Watches the documentation source folder and regenerates it on every file change"
    Target.create "KeepGeneratingDocs" keepGeneratingDocs
    Target.description "Generates the website for the project - for release"
    Target.create "GenerateDocs" generateDocsRelease
    Target.description "Generates the website for the project - for local use"
    Target.create "GenerateDocsDebug" generateDocsDebug
    Target.description "Makes a tag on the current commit, and a GitHub release afterwards"
    Target.create "GitHubRelease" githubRelease

    Target.description "Publishes the documentation and makes a GitHub release"
    Target.create "Release" ignore

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
        ==> "NuGetPack"
        ==> "NuGetPublish"
        ==>! "GitHubRelease"

    "CheckForReleaseCredentials"
        ==> "CheckForReleaseNotesDate"
        ==> "GitHubRelease"
        ==>! "Release"

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fs"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext
    initTargets()
    Target.runOrDefaultWithArguments "NuGetPack"
    0
