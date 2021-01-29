// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.DesigntimeFarkleTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammar
open Farkle.Parser
open FsCheck
open System
open System.Collections.Immutable

type TestClass = TestClass of string
with
    member x.ClassInstance(_: ITransformerContext, _: ReadOnlySpan<char>) =
        match x with TestClass x -> x
    static member ClassStatic (TestClass x, _: ITransformerContext, _: ReadOnlySpan<char>) = x

[<Struct>]
type TestStruct = TestStruct of string
with
    member x.StructInstance(_: ITransformerContext, _: ReadOnlySpan<char>) =
        match x with TestStruct x -> x

[<Tests>]
let tests = testList "Designtime Farkle tests" [
    test "A nonterminal with no productions gives an error" {
        let nt = nonterminal "Vacuous"
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError = Error [BuildError.EmptyNonterminal "Vacuous"]
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
            [BuildError.DuplicateProduction(Nonterminal(0u, nt.Name), ImmutableArray.Create(LALRSymbol.Terminal <| Terminal(0u, term.Name)))]
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
        let result = RuntimeFarkle.parseString runtime ""

        Expect.isOk result "Something went wrong"
    }

    test "A grammar with a nullable terminal is not accepted" {
        let designtime =
            let term = terminal "Nullable" (T(fun _ _ -> ())) (Regex.chars Number |> Regex.atLeast 0)
            "S" ||= [!% term =% ()]
        let grammar = DesigntimeFarkleBuild.build designtime |> fst
        Expect.equal grammar (Error [BuildError.NullableSymbol (Choice1Of4 <| Terminal(0u, "Nullable"))])
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

    test "Designtime Farkles, post-processors, transformers and fusers are covariant" {
        let df = Terminals.string '"' "String"
        let t = T(fun _ x -> x.ToString())
        let tInt = T(fun _ _ -> 380)
        let f = Builder.F(fun x -> x.ToString())
        let fInt = Builder.F(fun _ -> 286)
        Expect.isSome (tryUnbox<DesigntimeFarkle<obj>> df) "Designtime Farkles are not covariant"
        Expect.isSome (tryUnbox<PostProcessor<obj>> PostProcessors.ast) "Post-processors are not covariant"
        Expect.isSome (tryUnbox<T<obj>> t) "Transformers are not covariant"
        Expect.isNone (tryUnbox<T<obj>> tInt) "Transformers on value types are covariant while they shouldn't"
        Expect.isSome (tryUnbox<Builder.F<obj>> f) "Fusers are not covariant"
        Expect.isNone (tryUnbox<Builder.F<obj>> fInt) "Fusers on value types are covariant while they shouldn't"
    }

    test "Farkle can properly handle line groups" {
        let runtime =
            Group.Line("Line Group", "!!", fun _ data -> data.ToString())
            |> RuntimeFarkle.build
        Expect.equal (runtime.Parse "!! No new line") (Ok "!! No new line")
            "Farkle does not properly handle line groups that end on EOF"
        Expect.equal (runtime.Parse "!! Has new line\n") (Ok "!! Has new line")
            "Farkle does not properly handle line groups that end on a new line"
    }

    test "Farkle can properly handle block groups" {
        let runtime =
            Group.Block("Block Group", "{", "}", fun _ data -> data.ToString())
            |> RuntimeFarkle.build

        Expect.equal (runtime.Parse "{🆙🆙}") (Ok "{🆙🆙}") "Farkle does not properly handle block groups"
    }

    test "Renaming designtime Farkles works" {
        let runtime =
            Terminals.int "Number"
            |> DesigntimeFarkle.rename "Integer"
            |> RuntimeFarkle.build

        let grammar = runtime.GetGrammar()
        let (Nonterminal(_, startSymbolName)) = grammar.StartSymbol

        Expect.equal startSymbolName "Integer" "Renaming a designtime Farkle had no effect"
    }

    test "Many block groups can be ended by the same symbol" {
        // It doesn't cause a DFA conflict because the
        // end symbols of the different groups are considered equal.
        let runtime =
            "Test" ||= [
                !% Group.Block("Group 1", "{", "}", fun _ _ -> ()) => id
                !% Group.Block("Group 2", "[", "}", fun _ _ -> ()) => id
            ]
            |> RuntimeFarkle.buildUntyped

        runtime.GetGrammar() |> ignore
    }

    test "Parsing untyped groups works" {
        let runtime =
            "Test" ||= [
                !% Group.Block("Untyped Group", "{", "}") =% ()
            ]
            |> RuntimeFarkle.build

        Expect.isOk (runtime.Parse "{test}") "Parsing a test string failed"
    }

    test "The dynamic post-processor works with various kinds of delegates" {
        let magic = Guid.NewGuid().ToString()
        let testClass = magic |> TestClass |> box
        let testStruct = magic |> TestStruct |> box
        let testData = [
            "ClassInstance", typeof<TestClass>, testClass
            "ClassStatic", typeof<TestClass>, testClass
            "StructInstance", typeof<TestStruct>, testStruct
            // A StructStatic like ClassStatic above is not supported.
            // See https://github.com/dotnet/dotnet-api-docs/pull/5141
        ]

        let mkTerminal (name, typ: Type, target) =
            let t = typ.GetMethod(name).CreateDelegate<T<string>>(target)
            terminal name t (Regex.string name)
        let runtime =
            "Test" ||=
                List.map (fun x -> !@ (mkTerminal x) => id) testData
            |> RuntimeFarkle.build

        // We will run the tests many times to ensure the dynamic post-rpocessors are created.
        for i = 1 to 100 do
            if i % 10 = 0 then
                // We give some extra time for the asynchronously created dynamic post-processor to get ready.
                Threading.Thread.Sleep(1)
            for x, _, _ in testData do
                Expect.equal (RuntimeFarkle.parseString runtime x) (Ok magic) (sprintf "%s was not parsed correctly" x)
    }

    test "Parser application errors are correctly handled" {
        let terminal =
            Regex.string "O"
            |> terminal "Terminal" (T(fun _ _ -> error "Terminal found" |> ignore))
        let designtime =
            "Nonterminal" ||= [!@ terminal => id; empty => (fun () -> error "Empty input")]
        let runtime = designtime.Build()

        let mkError column msg =
            ParserError(Position.Create 1UL column (column - 1UL), ParseErrorType.UserError msg)
            |> FarkleError.ParseError |> Error

        // TODO: Fix the positions
        let error1 = mkError 9UL "Terminal found"
        Expect.equal (runtime.Parse "       O") error1 "Application errors at transformers were not caught."

        let error2 = mkError 4UL "Empty input"
        Expect.equal (runtime.Parse "   ") error2 "Application errors at fusers were not caught."
    }

    test "Farkle does not overflow the stack when processing a deep designtime Farkle" {
        let depth = 1000
        let nonterminals = Array.init depth (sprintf "N%d" >> nonterminalU)

        for i = 0 to nonterminals.Length - 2 do
            nonterminals.[i].SetProductions(!% nonterminals.[i + 1])
        nonterminals.[nonterminals.Length - 1].SetProductions(ProductionBuilder "x")

        let grammar =
            DesigntimeFarkleBuild.createGrammarDefinition nonterminals.[0]
            |> DesigntimeFarkleBuild.buildGrammarOnly
        Expect.isOk grammar "Building failed"
    }
]
