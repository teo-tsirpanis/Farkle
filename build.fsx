// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake.IO
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git

open System
open System.IO

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docsrc/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Farkle"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "A LALR parsing toolkit for F#."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "Farkle is a port of GOLD Parser 5.0 for F#, but aims to be buch more than that. It's still under early development."

// List of author names (for NuGet package)
let authors = [ "Theodore Tsirpanis" ]

// Tags for your project (for NuGet package)
let tags = "parser lalr gold-parser"

// File system information
let solutionFile  = "Farkle.sln"

// nuspec files. These describe the metapackages.
let nuspecs = !! "src/*.nuspec"

// Default target configuration
let configuration = DotNet.BuildConfiguration.Release
let configurationAsString = "Release"

let exeFramework = "netcoreapp2.1"

let sourceProjects = !! "src/**/*.??proj"

let projects = !! "**/*.??proj" -- "**/*.shproj"

// Pattern specifying assemblies to be tested
let testAssemblies = !! ("bin/*Tests*/" </> exeFramework </> "*Tests*.dll")
// Additional command line arguments passed to Expecto.
let testArguments = ""

// Pattern specifying assemblies to be benchmarked
let benchmarkAssemblies = !! ("bin/*Benchmarks*/" </> exeFramework </> "*Benchmarks*.dll")
// Additional command line arguments passed to BenchmarkDotNet.
let benchmarkArguments = ""

let nugetPackages = !! "bin/*.nupkg"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "teo-tsirpanis"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = "Farkle"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" "https://raw.githubusercontent.com/teo-tsirpanis"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [
            AssemblyInfo.Title projectName
            AssemblyInfo.Product project
            AssemblyInfo.Description summary
            AssemblyInfo.Version release.AssemblyVersion
            AssemblyInfo.FileVersion release.AssemblyVersion
            AssemblyInfo.Configuration (sprintf "%A" configuration)
        ]

    let getProjectDetails projectPath =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        (projectPath, projectName, Path.GetDirectoryName projectPath, getAssemblyInfoAttributes projectName)

    sourceProjects
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// Copies binaries from default VS location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target.create "CopyBinaries" (fun _ ->
    Shell.cleanDir "bin"
    projects
    |>  Seq.map (fun f -> ((Path.GetDirectoryName f) </> "bin" </> configurationAsString, "bin" </> (Path.GetFileNameWithoutExtension f)))
    |>  Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

let vsProjFunc x =
    {x with
        DotNet.BuildOptions.Configuration = configuration}

Target.create "Clean" (fun _ ->
    DotNet.exec id "clean" "" |> ignore
    Shell.cleanDirs ["bin"; "temp"]
)

Target.create "CleanDocs" (fun _ -> Shell.cleanDir "docs")

// --------------------------------------------------------------------------------------
// Build library & test project

Target.create "Build" (fun _ ->
    Shell.copyFile "Directory.Build.props" "Directory.Build.xml"
    File.applyReplace (release.Notes |> String.concat Environment.NewLine |> String.replace "@ReleaseNotes") "Directory.Build.props"
    DotNet.build vsProjFunc solutionFile
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target.create "RunTests" (fun _ ->
    testAssemblies
    |> Seq.iter (fun x -> DotNet.exec (fun p -> {p with WorkingDirectory = Path.GetDirectoryName x}) x testArguments |> ignore)
)

Target.create "Benchmark" (fun _ ->
    benchmarkAssemblies
    |> Seq.iter (fun x -> DotNet.exec (fun p -> {p with WorkingDirectory = Path.GetDirectoryName x}) x benchmarkArguments |> ignore)
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    sourceProjects
    |> Seq.iter (
        DotNet.pack (fun p ->
            {p with
                Configuration = configuration
                OutputPath = __SOURCE_DIRECTORY__ @@ "bin" |> Some
                // AdditionalArgs =
                // [
                //     release.NugetVersion |> sprintf "/p:PackageVersion=%s"
                // ]
            } |> DotNet.Options.withCustomParams (Some <| sprintf "/p:PackageVersion=%s"release.NugetVersion)
        )
    )
)

Target.create "PublishNuget" (fun _ ->
    Paket.push(fun p ->
        {p with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "bin" })
)


// --------------------------------------------------------------------------------------
// Generate the documentation

// --------------------------------------------------------------------------------------
// Generate the documentation

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "bin"
let content    = __SOURCE_DIRECTORY__ @@ "docsrc/content"
let output     = __SOURCE_DIRECTORY__ @@ "docs"
let files      = __SOURCE_DIRECTORY__ @@ "docsrc/files"
let templates  = __SOURCE_DIRECTORY__ @@ "docsrc/tools/templates"
let formatting = __SOURCE_DIRECTORY__ @@ "packages/build/FSharp.Formatting"
let toolpath = __SOURCE_DIRECTORY__ @@ "packages/build/FSharp.Formatting.CommandTool/tools/fsformatting.exe"
let docTemplate = "docpage.cshtml"

let github_release_user = Environment.environVarOrDefault "github_release_user" gitOwner
let githubLink = sprintf "https://github.com/%s/%s" github_release_user gitName

let root =
    function
    | true -> "/Farkle"
    | false -> "file://" + (__SOURCE_DIRECTORY__ @@ "../../docs")

// Specify more information about your project
let info =
    [
        "project-name", project
        "project-author", String.concat ", " authors
        "project-summary", summary
        "project-github", githubLink
        "project-nuget", "http://nuget.org/packages/Farkle"
    ]

let referenceBinaries = []

let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[   templates;
                            formatting @@ "templates"
                            formatting @@ "templates/reference" ])

let referenceDocs isRelease =
    Directory.ensure (output @@ "reference")

    let binaries () =
        let manuallyAdded =
            referenceBinaries
            |> List.map (fun b -> bin @@ b)

        let conventionBased =
            bin
            |> DirectoryInfo
            |> DirectoryInfo.getSubDirectories
            |> Array.map (fun d ->
                let net472Bin = DirectoryInfo.getSubDirectories d |> Array.filter(fun x -> x.FullName.ToLower().Contains("net47"))
                d.Name, net472Bin.[0])
            |> Array.map (fun (name, d) ->
                d.GetFiles()
                |> Array.filter (fun x ->
                    x.Name.ToLower() = (sprintf "%s.dll" name).ToLower())
                |> Array.map (fun x -> x.FullName)
                )
            |> Array.concat
            |> List.ofArray

        conventionBased @ manuallyAdded

    binaries()
    |> FSFormatting.createDocsForDlls (fun args ->
        { args with
            OutputDirectory = output @@ "reference"
            LayoutRoots =  layoutRootsAll.["en"]
            ProjectParameters =  ("root", root isRelease)::info
            SourceRepository = githubLink @@ "tree/master"
            ToolPath = toolpath}
    )


let copyFiles () =
    Shell.copyRecursive files output true
    |> Trace.logItems "Copying file: "
    Directory.ensure (output @@ "content")
    Shell.copyRecursive (formatting @@ "styles") (output @@ "content") true
    |> Trace.logItems "Copying styles and scripts: "

let docs isRelease =
    File.delete "docsrc/content/release-notes.md"
    Shell.copyFile "docsrc/content/" "RELEASE_NOTES.md"
    Shell.rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"

    File.delete "docsrc/content/license.md"
    Shell.copyFile "docsrc/content/" "LICENSE.txt"
    Shell.rename "docsrc/content/license.md" "docsrc/content/LICENSE.txt"


    DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath templates)
    |> Seq.iter (fun d ->
                    let name = d.Name
                    if name.Length = 2 || name.Length = 3 then
                        layoutRootsAll.Add(
                                name, [templates @@ name
                                       formatting @@ "templates"
                                       formatting @@ "templates/reference" ]))
    copyFiles ()

    for dir in  [ content; ] do
        let langSpecificPath(lang, path:string) =
            path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.exists(fun i -> i = lang)
        let layoutRoots =
            let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
            match key with
            | Some lang -> layoutRootsAll.[lang]
            | None -> layoutRootsAll.["en"] // "en" is the default language

        FSFormatting.createDocs (fun args ->
            { args with
                Source = content
                OutputDirectory = output
                LayoutRoots = layoutRoots
                ProjectParameters  = ("root", root isRelease)::info
                Template = docTemplate
                ToolPath = toolpath})

Target.create "KeepRunning" (fun _ ->
    use watcher = !! "docsrc/content/**/*.*" |> ChangeWatcher.run (fun changes ->
        docs false
    )

    Trace.traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore
)

Target.create "GenerateDocs" (fun _ -> !! "./docs/**" |> Zip.zip "docs" "docs.zip")
Target.create "GenerateHelp" (fun _ -> docs true)
Target.create "GenerateHelpDebug" (fun _ -> docs false)

Target.create "GenerateReferenceDocs" (fun _ -> referenceDocs true)
Target.create "GenerateReferenceDocsDebug" (fun _ -> referenceDocs false)

Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Shell.copyRecursive "docs" tempDocsDir true |> Trace.tracefn "Copied %A"
    Staging.stageAll tempDocsDir
    Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)


// --------------------------------------------------------------------------------------
// Release Scripts

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target.create "Release" (fun _ ->
    let user =
        match Environment.environVarOrDefault "github-user" String.Empty with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserInput "Username: "
    let pw =
        match Environment.environVarOrDefault "github-pw" String.Empty with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "Password: "
    let remote =
        CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (String.endsWith "(push)")
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Staging.stageAll ""
    Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion

    // release on github
    createClient user pw
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    |> uploadFiles nugetPackages
    |> uploadFile ("./src/Farkle/FSharp - Farkle.pgt")
    |> releaseDraft
    |> Async.RunSynchronously
)

Target.create "BuildPackage" ignore

Target.create "CI" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

open Fake.Core.TargetOperators

"CleanDocs"
    ==> "Clean"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "CopyBinaries"
    ==> "RunTests"
    ==> "GenerateReferenceDocs"
    ==> "GenerateDocs"
    ==> "NuGet"
    ==> "BuildPackage"
    ==> "All"
    ==> "CI"

"CleanDocs"
    ==> "GenerateHelp"
    ==> "GenerateReferenceDocs"
    ==> "GenerateDocs"
    ==> "ReleaseDocs"

"CleanDocs"
    ==> "GenerateHelpDebug"
    ==> "GenerateReferenceDocsDebug"
    ==> "KeepRunning"

"CopyBinaries"
    ==> "Benchmark"
    ==> "CI"

"ReleaseDocs"
    ==> "Release"

"BuildPackage"
    ==> "PublishNuget"
    ==> "Release"

Target.runOrDefault "RunTests"
