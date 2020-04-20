// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.DesigntimeFarkleTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammar
open FsCheck
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

    test "Duplicate literals do not give an error" {
        let nt = "Colliding" ||= [
            !% (literal "a") =% 1
            !% (literal "a") .>> literal "b" =% 2
        ]
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        Expect.isOk result "Duplicate literals give an error"
    }

    test "A grammar that only accepts the empty string indeed accepts it" {
        let designtime = "S" ||= [empty =% ()]
        let runtime = RuntimeFarkle.build designtime
        let result = RuntimeFarkle.parse runtime ""

        Expect.isOk result "Something went wrong"
    }

    test "A grammar with a nullable terminal is not accepted" {
        let designtime =
            let term = terminal "Nullable" (T(fun _ _ -> ())) (Regex.chars Number |> Regex.atLeast 0)
            "S" ||= [!% term =% ()]
        let grammar = DesigntimeFarkleBuild.build designtime |> fst
        Expect.equal grammar (Error (BuildError.NullableSymbol (Choice1Of4 <| Terminal(0u, "Nullable"))))
            "A grammar with a nullable symbol was accepted"
    }

    test "Farkle loudly rejects an Accept-Reduce error which is silently accepted by GOLD Parser" {
        let designtime =
            let S = nonterminal "S"
            let T = "T" ||= [!% S =% ()]
            S.SetProductions(
                !& "x" =% (),
                !% T =% ()
            )
            S
        let grammar = DesigntimeFarkleBuild.build designtime |> fst
        Expect.isError grammar "An Accept-Reduce error was silently accepted by Farkle too"
    }

    test "DesigntimeFarkle objects have the correct equality semantics" {
        let lit1 = literal "Test"
        let lit2 = literal "Test"
        Expect.isTrue (lit1 = lit2) "Literal DesigntimeFarkle objects are not checked for structural equality"

        let t1 = terminal "Test" (T(fun _ _ -> null)) (Regex.string "Test")
        let t2 = terminal "Test" (T(fun _ _ -> null)) (Regex.string "Test")
        Expect.isFalse (t1 = t2) "DesigntimeFarkle terminals are not checked for reference equality"

        let nont1 = nonterminal "Test" :> DesigntimeFarkle
        let nont2 = nonterminal "Test" :> DesigntimeFarkle
        Expect.isFalse (nont1 = nont2) "DesigntimeFarkle nonterminals are not checked for reference equality"
    }

    testProperty "Farkle can properly read signed integers" (fun num ->
        let runtime = Terminals.int64 "Signed" |> RuntimeFarkle.build
        Expect.equal (runtime.Parse(string num)) (Ok num) "Parsing a signed integer failed")

    testProperty "Farkle can properly read unsigned integers" (fun num ->
        let runtime = Terminals.uint64 "Unsigned" |> RuntimeFarkle.build
        Expect.equal (runtime.Parse(string num)) (Ok num) "Parsing an unsigned integer failed")

    testProperty "Farkle can properly read floating-point numbers" (fun (NormalFloat num) ->
        let runtime = Terminals.float "Floating-point" |> RuntimeFarkle.build
        Expect.equal (runtime.Parse(string num)) (Ok num) "Parsing an unsigned integer failed")

    test "Designtime Farkles, post-processors and transformer callbacks are covariant" {
        let df = "Sbubby" ||= [!& "Eef" =% "Freef"]
        let t = T(fun _ x -> x.ToString())
        let tInt = T(fun _ _ -> 380)
        Expect.isSome (tryUnbox<DesigntimeFarkle<obj>> df) "Designtime Farkles are not covariant"
        Expect.isSome (tryUnbox<PostProcessor<obj>> PostProcessors.ast) "Post-processors are not covariant"
        Expect.isSome (tryUnbox<T<obj>> t) "Transformer callbacks are not covariant"
        Expect.isNone (tryUnbox<T<obj>> tInt) "Transformer callbacks on value types are covariant while they shouldn't"
    }

    test "Farkle can properly handle line groups" {
        let runtime =
            Group.Line("LineGroup", "!!", T(fun _ data -> data.ToString()))
            |> RuntimeFarkle.build
        Expect.equal (runtime.Parse "!! No new line") (Ok "!! No new line")
            "Farkle does not properly handle line groups that end on EOF"
        Expect.equal (runtime.Parse "!! Has new line\n") (Ok "!! Has new line")
            "Farkle does not properly handle line groups that end on a new line"
    }

    test "Farkle can properly handle block groups" {
        let runtime =
            Group.Block("Block Group", "{", "}", T(fun _ data -> data.ToString()))
            |> RuntimeFarkle.build

        Expect.equal (runtime.Parse "{🆙🆙}") (Ok "{🆙🆙}") "Farkle does not properly handle block groups"
    }

    test "Renaming designtime Farkles works" {
        let runtime =
            Terminals.int "Number"
            |> DesigntimeFarkle.rename "Integer"
            |> RuntimeFarkle.build

        let grammar = runtime.GetGrammar()

        Expect.equal grammar.StartSymbol.Name "Integer" "Renaming a designtime Farkle had no effect"
    }
]
