// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "paket: groupref FakeBuild //"
#nowarn "3180" // Mutable locals allocated as reference cells.

#if !FAKE
// Because intellisense.fsx would be loaded twice, we have to put the ifdef ourselves.
#load "./.fake/build.fsx/intellisense_lazy.fsx"
#endif

open Fake.Api
open Fake.BuildServer
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git
open Scriban
open System
open System.IO
open System.Runtime.InteropServices
open System.Text.RegularExpressions

Target.initEnvironment()

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
let testProject = "./tests/Farkle.Tests/Farkle.Tests.fsproj"

let msBuildTestProject = "./tests/Farkle.Tools.MSBuild.Tests/Farkle.Tools.MSBuild.Tests.csproj"

// Additional command line arguments passed to Expecto.
let testArguments = "--nunit-summary TestResults.xml"

let localPackagesFolder = "./tests/packages/"

let projects = !! "**/*.??proj" -- "**/*.shproj"

// The project to be benchmarked
let benchmarkProject = "./tests/Farkle.Benchmarks/Farkle.Benchmarks.fsproj"

// Additional command line arguments passed to BenchmarkDotNet.
let benchmarkArguments = "-f * --memory true -e github"

let benchmarkReports = !! (Path.getDirectory benchmarkProject @@ "BenchmarkDotNet.Artifacts/results/*-report-github.md")

let benchmarkReportsDirectory = "./performance/"

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
let releaseInfo = ReleaseNotes.load "./RELEASE_NOTES.md"

let releaseNotes =
    let lines s = seq {
        use sr = new StringReader(if isNull s then "" else s)
        let mutable s = ""
        s <- sr.ReadLine()
        while not <| isNull s do
            yield s
            s <- sr.ReadLine()
    }
    match BuildServer.buildServer with
    | AppVeyor ->
        sprintf "This is a build from the commit with id: %s from branch %s/%s"
            AppVeyor.Environment.RepoCommit
            AppVeyor.Environment.RepoName
            AppVeyor.Environment.RepoBranch
        :: AppVeyor.Environment.RepoCommitMessage
        :: (AppVeyor.Environment.RepoCommitMessageExtended |> lines |> List.ofSeq)
    | _ -> releaseInfo.Notes

let nugetVersion =
    match BuildServer.buildServer with
    AppVeyor -> sprintf "%s-ci.%s" releaseInfo.NugetVersion AppVeyor.Environment.BuildNumber
    | _ -> releaseInfo.NugetVersion

BuildServer.install [AppVeyor.Installer]

let githubToken = lazy(Environment.environVarOrFail "farkle-github-token")
let nugetKey = lazy(Environment.environVarOrFail "NUGET_KEY")

Target.description "Fails the build if the appropriate environment variables for the release do not exist"
Target.create "CheckForReleaseCredentials" (fun _ ->
    githubToken.Value |> ignore
    nugetKey.Value |> ignore)

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

Target.description "Cleans the output documentation directory"
Target.create "CleanDocs" (fun _ -> Shell.cleanDir "output")

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

Target.description "Runs the unit tests"
Target.create "RunTests" (fun _ ->
    dotNetRun testProject None DotNet.BuildConfiguration.Debug "" testArguments
    Trace.publish (ImportData.Nunit NunitDataVersion.Nunit) (Path.getDirectory testProject @@ "TestResults.xml")
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

Target.description "Runs all tests"
Target.create "Test" ignore

let shouldCIBenchmark =
    match BuildServer.buildServer with
    | LocalBuild -> true
    | AppVeyor ->
        let releaseNotesAsString = AppVeyor.Environment.RepoCommitMessage + "\n" + AppVeyor.Environment.RepoCommitMessageExtended
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        && (AppVeyor.Environment.IsReBuild = "true" || releaseNotesAsString.Contains("!BENCH!"))
    | _ -> true

Target.description "Runs all benchmarks"
Target.create "Benchmark" (fun _ ->
    dotNetRun benchmarkProject None DotNet.BuildConfiguration.Release "" benchmarkArguments
    Seq.iter pushArtifact benchmarkReports)

Target.description "Adds the benchmark results to the appropriate folder"
Target.create "AddBenchmarkReport" (fun _ ->
    let reportFileName x = benchmarkReportsDirectory @@ (sprintf "%s.%s.md" x nugetVersion)
    Directory.ensure benchmarkReportsDirectory
    Trace.logItems "Benchmark reports: " benchmarkReports
    benchmarkReports
    |> Seq.iter (fun x ->
        let newFn = Regex.Replace(Path.GetFileName x, @"Farkle\.Benchmarks\.(\w+)-report-github\.md", "$1") |> reportFileName
        Shell.copyFile newFn x
        File.applyReplace (String.replace ";" ",") newFn
    )
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

Target.description "Publishes the NuGet packages"
Target.create "NuGetPublish" (fun _ ->
    Seq.iter (DotNet.nugetPush (fun p ->
        {p with
            PushParams =
                {p.PushParams with
                    Source = Some "https://www.nuget.org"
                    ApiKey = Some nugetKey.Value
                }
        }
    )) nugetPackages
)

// --------------------------------------------------------------------------------------
// Generate the documentation

let referenceDocsTempPath = __SOURCE_DIRECTORY__ @@ "temp/referencedocs-publish"
let docsOutput = __SOURCE_DIRECTORY__ @@ "output/"

let root isRelease =
    match isRelease with
    | true -> ""
    | false -> "--parameters root \"file://" + docsOutput.Replace("\\", "/") + "\""

let moveFileTemporarily src dest =
    File.Copy(src, dest, true)
    {new IDisposable with member _.Dispose() = File.delete dest}

let generateDocs doWatch isRelease =
    use __ = moveFileTemporarily "RELEASE_NOTES.md" "docs/release-notes.md"
    use __ = moveFileTemporarily "LICENSE.txt" "docs/license.md"

    let fsDocsCommand = if doWatch then "watch" else "build"
    let root = root isRelease

    (sprintf "%s --clean --output \"%s\" --properties FsFormatting=true %s" fsDocsCommand docsOutput root)
    |> DotNet.exec id "fsdocs"
    |> handleFailure

Target.description "Prepares the reference documentation generator"
Target.create "PrepareDocsGeneration" (fun _ ->
    DotNet.build (fun p ->
        {p with
            Configuration = documentationConfiguration}
    ) farkleProject
    DotNet.publish (fun p ->
        {p with
            Framework = Some DocumentationAssemblyFramework
            Configuration = documentationConfiguration
            OutputPath = Some referenceDocsTempPath
            NoBuild = true}
    ) farkleProject
)

Target.description "Watches the documentation source folder and regenerates it on every file change"
Target.create "KeepGeneratingDocs" (fun _ ->
    generateDocs true false
)

Target.description "Generates the website for the project - for release"
Target.create "GenerateDocs" (fun _ ->
    generateDocs false true
    !! (docsOutput @@ "**") |> Zip.zip docsOutput "docs.zip"
    Trace.publish ImportData.BuildArtifact "docs.zip"
)
Target.description "Generates the website for the project - for local use"
Target.create "GenerateDocsDebug" (fun _ -> generateDocs false false)

Target.description "Releases the documentation to GitHub Pages."
Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    // Some files might no longer exist; better delete them all before the copy.
    !! "temp/gh-pages/**" -- "temp/gh-pages/.git/**" |> File.deleteAll
    Shell.copyRecursive docsOutput tempDocsDir true |> Trace.tracefn "Copied %A"
    Staging.stageAll tempDocsDir
    Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" nugetVersion)
    Branches.push tempDocsDir
)

// --------------------------------------------------------------------------------------
// Release Scripts

let remoteToPush = lazy (
    CommandHelper.getGitResult "" "remote -v"
    |> Seq.filter (String.endsWith "(push)")
    |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
    |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0])

Target.description "Publishes the benchmark report."
Target.create "PublishBenchmarkReport" (fun _ ->
    !! "performance/**" |> Seq.iter (Staging.stageFile "" >> ignore)
    Commit.exec "" (sprintf "Publish performance reports for version %s" nugetVersion)
    Branches.pushBranch "" remoteToPush.Value (Information.getBranchName "")
)

Target.description "Makes a tag on the current commit, and a GitHub release afterwards."
Target.create "GitHubRelease" (fun _ ->

    Branches.tag "" nugetVersion
    Branches.pushTag "" remoteToPush.Value nugetVersion

    GitHub.createClientWithToken githubToken.Value
    |> GitHub.createRelease gitOwner gitName nugetVersion
        (fun x ->
            {x with
                Name = sprintf "Version %s" nugetVersion
                Prerelease = releaseInfo.SemVer.PreRelease.IsSome
                Body = releaseNotes |> Seq.map (sprintf "* %s") |> String.concat Environment.NewLine})
    |> GitHub.uploadFiles releaseArtifacts
    |> GitHub.publishDraft
    |> Async.RunSynchronously
)

Target.description "The CI generates the documentation, the NuGet packages, \
and uploads them as artifacts, along with the benchmark report."
Target.create "CI" ignore

Target.description "Publishes the documentation and makes a GitHub release"
Target.create "Release" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build target <Target>' to override

"Clean"
    ==> "GenerateCode"

["RunTests"; "PrepareMSBuildTests"; "NuGetPack"; "Benchmark"; "PrepareDocsGeneration"]
|> List.iter (fun target -> "GenerateCode" ==> target |> ignore)

["RunMSBuildTestsNetCore"; "RunMSBuildTestsNetFramework"]
|> List.iter (fun target -> "PrepareMSBuildTests" ==> target |> ignore)

"Test" <== ["RunTests"; "RunMSBuildTestsNetCore"]

"RunMSBuildTestsNetFramework"
    =?> ("Test", RuntimeInformation.IsOSPlatform(OSPlatform.Windows))

"Test"
    ==> "NuGetPack"
    ==> "CI"

[""; "Debug"]
|> List.iter (fun x ->
    "CleanDocs"
        ==> "PrepareDocsGeneration"
        ==> (sprintf "GenerateDocs%s" x) |> ignore)

"GenerateDocs"
    ==> "ReleaseDocs"

"GenerateDocs"
    ==> "CI"

"PrepareDocsGeneration"
    ==> "KeepGeneratingDocs"

"Benchmark"
    ==> "AddBenchmarkReport"
    ==> "PublishBenchmarkReport"

// I want a clean repo when the packages are going to be built.
"NuGetPublish"
    ?=> "AddBenchmarkReport"

"Clean"
    ==> "NuGetPack"
    ==> "NuGetPublish"
    ==> "GitHubRelease"

"CI" <== ["NuGetPack"; "GenerateDocs"]
"Benchmark" =?> ("CI", shouldCIBenchmark)

"CheckForReleaseCredentials"
    ==> "GitHubRelease"

"Release" <== ["GitHubRelease"; "ReleaseDocs"]

Target.runOrDefault "NuGetPack"
