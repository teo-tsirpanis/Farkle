// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CommonTests

open Expecto
open Farkle.Common
open System
open System.Threading
open System.Threading.Tasks

[<Tests>]
let tests = testList "Farkle.Common tests" [
    test "A latch can be set only once" {
        for __ = 1 to 10 do
            let mutable latch = Latch.Create false
            let mutable enteredCount = 0
            // We want all the threads to start roughly at the same time.
            use ev = ManualResetEventSlim()
            let tasks = Array.init (Environment.ProcessorCount * 4) (fun _ -> Task.Factory.StartNew(Action(fun () ->
                ev.Wait()
                if latch.TrySet() then
                    Interlocked.Increment(&enteredCount) |> ignore), TaskCreationOptions.LongRunning))
            ev.Set()
            Task.WaitAll tasks

            Expect.equal enteredCount 1 "A latch was not set only once"
    }
]
