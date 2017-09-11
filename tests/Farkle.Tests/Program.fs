// Learn more about F# at http://fsharp.org

open System
open Expecto
open FsCheck

[<EntryPoint>]
let main argv = 
    Arb.register<Farkle.Tests.Generators.Generators>() |> ignore
    runTestsInAssembly defaultConfig argv
