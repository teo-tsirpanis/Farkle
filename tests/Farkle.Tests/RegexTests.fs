// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegexTests

open Expecto
open Farkle.Builder
open Farkle.Tests
open FsCheck

[<Tests>]
let tests = testList "Regex tests" [
    testProperty "Regex.optional is idempotent" (fun regex ->
        let opt1 = Regex.optional regex
        let opt2 = Regex.optional opt1
        opt1 = opt2
    )

    testProperty "Regex.ZeroOrMore is idempotent" (fun regex ->
        let star1 = Regex.atLeast 0 regex
        let star2 = Regex.atLeast 0 star1
        star1 = star2)

    testProperty "Chaining Regex.And works the same with Regex.Concat" (fun regexes ->
        let chained = Array.fold (<&>) Regex.Empty regexes
        let concatenated = Regex.Join regexes
        chained = concatenated |@ (sprintf "\nChained: %A\nConcatenated = %A" chained concatenated)
    )
]
