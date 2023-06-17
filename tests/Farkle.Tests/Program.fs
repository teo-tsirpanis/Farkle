// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Expecto
open Farkle.Tests
open FsCheck

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
            |> function null -> "null" | x -> x.ToJsonString()
        System.IO.File.WriteAllText(jsonOutputPath, jsonContents)
        printfn "Done."
        0
    | argv -> runTestsInAssemblyWithCLIArgs [] argv
