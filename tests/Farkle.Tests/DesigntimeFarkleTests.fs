// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.DesigntimeFarkleTests

open Expecto
open Farkle.Builder
open Farkle.Grammar
open System.Collections.Immutable

[<Tests>]
let tests = testList "Designtime Farkle tests" [
    test "A nonterminal with no productions gives an error" {
        let nt = nonterminal "Vacuous"
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError = "Vacuous" |> Set.singleton |> BuildError.EmptyNonterminals |> Error
        Expect.equal result expectedError "A nonterminal with no productions does not give an error"
    }

    test "A nonterminal with duplicate productions gives an error" {
        let term = literal "a"

        let nt = "Superfluous" ||= [
            // Returning the same numbers would still fail, but
            // this demonstrates why it is an error with Farkle, but
            // just a warning with GOLD Parser. In Farkle each production
            // has a fuser associated with it. While GOLD Parser would just merge
            // the duplicate productions and raise a warning, we can't do the same
            // because we can't choose between the fusers.
            !% term =% 1
            !% term =% 2
        ]
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError =
            (Nonterminal(0u, "Superfluous"), ImmutableArray.Empty.Add(LALRSymbol.Terminal <| Terminal(0u, "a")))
            |> Set.singleton
            |> BuildError.DuplicateProductions
            |> Error
        Expect.equal result expectedError "A nonterminal with duplicate productions does not give an error"
    }

    test "Duplicate literals give an error - TO BE FIXED" {
        let nt = "Colliding" ||= [
            !% (literal "a") =% 1
            !% (literal "a") .>> literal "b" =% 2
        ]
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError =
            [Terminal(0u, "a"); Terminal(1u, "a")]
            |> Seq.map Choice1Of4
            |> set
            |> BuildError.IndistinguishableSymbols
            |> Error
        Expect.equal result expectedError "Duplicate literals do not give an error"
    }
]
