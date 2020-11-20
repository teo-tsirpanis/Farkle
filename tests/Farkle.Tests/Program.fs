// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Chiron
open Expecto
open Farkle.Tests
open FsCheck
open System.Reflection
open System.Runtime.InteropServices

let jsonOutputPath = "../resources/generated.json"

let allTests =
    Assembly.GetExecutingAssembly().GetTypes()
    |> Seq.collect(fun t ->
        t.GetProperties(BindingFlags.Public ||| BindingFlags.Static)
        |> Seq.filter (fun prop ->
            prop.GetCustomAttributes()
            |> Seq.exists (fun attr -> attr :? TestsAttribute)
            && prop.PropertyType = typeof<Test>)
        |> Seq.map (fun prop -> prop.GetValue(null) :?> Test))
    |> List.ofSeq

[<EntryPoint>]
let main argv =
    match argv with
    | [|"generate-json"; size|] ->
        let seed = Random.newSeed()
        let size = int size
        printfn "Using random seed %A..." seed
        printfn "Generating JSON file of complexity %d..." size
        let jsonContents =
            Gen.eval size seed JsonGen
            |> Json.formatWith JsonFormattingOptions.Compact
        System.IO.File.WriteAllText(jsonOutputPath, jsonContents)
        printfn "Done."
        0
    | argv ->
        let tests = testList RuntimeInformation.FrameworkDescription allTests
        runTestsWithCLIArgs [] argv tests
