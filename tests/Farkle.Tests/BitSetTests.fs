// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.BitSetTests

open Expecto
open Farkle.Collections
open Farkle.Tests

[<Tests>]
let tests = testList "Bit set tests" [
    testProperty "A bit set can be reliably round-tripped into an integer sequence" (fun (x: BitSet) ->
        let xs = Array.ofSeq x
        let x' = BitSet.CreateRange xs
        Expect.equal x' x "The bit sets are different"
        let xs'= Array.ofSeq x'
        Expect.sequenceEqual xs' xs "The set's contents are different"
    )

    testProperty "The union of two bit sets works" (fun x1 x2 ->
        let indirect =
            let set1 = set x1
            let set2 = set x2
            Set.union set1 set2
            |> BitSet.CreateRange
        let direct = BitSet.Union(&x1, &x2)
        Expect.equal direct indirect "The union of the bit sets is wrong"
    )

    testProperty "The intersection of two bit sets works" (fun x1 x2 ->
        let indirect =
            let set1 = set x1
            let set2 = set x2
            Set.intersect set1 set2
            |> BitSet.CreateRange
        let direct = BitSet.Intersection(&x1, &x2)
        Expect.equal direct indirect "The union of the bit sets is wrong"
    )
]
