// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper

open System
open System.IO
open System.Diagnostics

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
let solutionFile  = !! "Farkle.sln"

// nuspec files. These describe the metapackages.
let nuspecs = !! "src/*.nuspec"

// Default target configuration
let configuration = "Release"

let exeFramework = "netcoreapp2.1"

let sourceProjects = !! "src/**/*.??proj"

let projects = !! "**/*.??proj" -- "**/*.shproj"

// Pattern specifying assemblies to be tested
let testAssemblies = !! ("bin/*Tests*/" </> exeFramework </> "*Tests*.dll")

// Pattern specifying assemblies to be benchmarked
let benchmarkAssemblies = !! ("bin/*Benchmarks*/" </> exeFramework </> "*Benchmarks*.dll")

let nugetPackages = !! "bin/*.nupkg"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "teo-tsirpanis"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = "Farkle"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.githubusercontent.com/teo-tsirpanis"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Description summary
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    sourceProjects
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// Copies binaries from default VS location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target "CopyBinaries" (fun _ ->
    CleanDir "bin"
    projects
    |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) </> "bin" </> configuration, "bin" </> (System.IO.Path.GetFileNameWithoutExtension f)))
    |>  Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

let vsProjFunc x =
    {x with
        DotNetCli.Configuration = configuration}

Target "Clean" (fun _ ->
    DotNetCli.RunCommand id "clean"
    CleanDirs ["bin"; "temp"]
)

Target "CleanDocs" (fun _ -> CleanDir "docs")

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    CopyFile "Directory.Build.props" "Directory.Build.xml"
    ReplaceInFile (release.Notes |> String.concat Environment.NewLine |> replace "@ReleaseNotes") "Directory.Build.props"
    DotNetCli.Restore id
    DotNetCli.Build vsProjFunc
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    testAssemblies
    |> Seq.iter (fun x -> DotNetCli.RunCommand (fun p -> {p with WorkingDir = Path.GetDirectoryName x}) x)
)

Target "Benchmark" (fun _ ->
    benchmarkAssemblies
    |> Seq.iter (fun x -> DotNetCli.RunCommand (fun p -> {p with WorkingDir = Path.GetDirectoryName x}) x)
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    sourceProjects
    |> Seq.iter (
        fun x -> DotNetCli.Pack (fun p ->
            {p with
                Project = x
                Configuration = configuration
                OutputPath = __SOURCE_DIRECTORY__ @@ "bin"
                AdditionalArgs =
                [
                    "--no-build"
                    release.NugetVersion |> sprintf "/p:PackageVersion=%s"
                ]}))
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        {p with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "bin" })
)


// --------------------------------------------------------------------------------------
// Generate the documentation

#load "docsrc/tools/generate.fsx"

// Documentation
Target "GenerateReferenceDocs" (fun _ ->
    FAKE.Generate.buildReference true
)

Target "GenerateReferenceDocsDebug" (fun _ ->
    FAKE.Generate.buildReference false
)

let generateHelp' fail debug =
    try
        FAKE.Generate.buildDocumentation (not debug)
        traceImportant "Help generated"
    with
    | _ when not fail ->
        traceImportant "generating help documentation failed"

let generateHelp fail =
    generateHelp' fail false

Target "GenerateHelp" (fun _ ->
    DeleteFile "docsrc/content/release-notes.md"
    CopyFile "docsrc/content/" "RELEASE_NOTES.md"
    Rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"

    DeleteFile "docsrc/content/license.md"
    CopyFile "docsrc/content/" "LICENSE.txt"
    Rename "docsrc/content/license.md" "docsrc/content/LICENSE.txt"

    generateHelp true
)

Target "GenerateHelpDebug" (fun _ ->
    DeleteFile "docsrc/content/release-notes.md"
    CopyFile "docsrc/content/" "RELEASE_NOTES.md"
    Rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"

    DeleteFile "docsrc/content/license.md"
    CopyFile "docsrc/content/" "LICENSE.txt"
    Rename "docsrc/content/license.md" "docsrc/content/LICENSE.txt"

    generateHelp' true true
)

Target "KeepRunning" (fun _ ->
    use watcher = !! "docsrc/content/**/*.*" |> WatchChanges (fun changes ->
        generateHelp' true true
    )

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.Dispose()
)

Target "GenerateDocs" (fun _ -> !! "./docs/**" |> Zip "docs" "docs.zip")

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    CopyRecursive "docs" tempDocsDir true |> tracefn "Copied %A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

let createIndexFsx lang =
    let content = """(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../../bin"

(**
F# Project Scaffold ({0})
=========================
*)
"""
    let targetDir = "docsrc/content" </> lang
    let targetFile = targetDir </> "index.fsx"
    ensureDirectory targetDir
    System.IO.File.WriteAllText(targetFile, String.Format(content, lang))

Target "AddLangDocs" (fun _ ->
    let args = System.Environment.GetCommandLineArgs()
    if args.Length < 4 then
        failwith "Language not specified."

    args.[3..]
    |> Seq.iter (fun lang ->
        if lang.Length <> 2 && lang.Length <> 3 then
            failwithf "Language must be 2 or 3 characters (ex. 'de', 'fr', 'ja', 'gsw', etc.): %s" lang

        let templateFileName = "template.cshtml"
        let templateDir = "docsrc/tools/templates"
        let langTemplateDir = templateDir </> lang
        let langTemplateFileName = langTemplateDir </> templateFileName

        if System.IO.File.Exists(langTemplateFileName) then
            failwithf "Documents for specified language '%s' have already been added." lang

        ensureDirectory langTemplateDir
        Copy langTemplateDir [ templateDir </> templateFileName ]

        createIndexFsx lang)
)

// --------------------------------------------------------------------------------------
// Release Scripts

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    let user =
        match getBuildParam "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (endsWith "(push)")
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
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

Target "BuildPackage" DoNothing

Target "CI" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

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

RunTargetOrDefault "RunTests"
