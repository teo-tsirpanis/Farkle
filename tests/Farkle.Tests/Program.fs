// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Chiron
open Expecto
open FsCheck
open Farkle.Tests

let jsonOutputPath = "../resources/generated.json"

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
    | argv -> runTestsInAssembly defaultConfig argv
