// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.DesigntimeFarkleTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammars
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

    test "Designtime Farkles, productions, post-processors, transformers and fusers are covariant" {
        let df = Terminals.string '"' "String"
        let prod = !& "x" =% ""
        let t = T(fun _ x -> x.ToString())
        let tInt = T(fun _ _ -> 380)
        let f = Builder.F(fun x -> x.ToString())
        let fInt = Builder.F(fun _ -> 286)
        Expect.isSome (tryUnbox<DesigntimeFarkle<obj>> df) "Designtime Farkles are not covariant"
        Expect.isSome (tryUnbox<Production<obj>> prod) "Productions are not covariant"
        Expect.isSome (tryUnbox<IPostProcessor<obj>> PostProcessors.ast) "Post-processors are not covariant"
        Expect.isSome (tryUnbox<T<obj>> t) "Transformers are not covariant"
        Expect.isNone (tryUnbox<T<obj>> tInt) "Transformers on value types are covariant while they shouldn't"
        Expect.isSome (tryUnbox<F<obj>> f) "Fusers are not covariant"
        Expect.isNone (tryUnbox<F<obj>> fInt) "Fusers on value types are covariant while they shouldn't"
    }

    test "The productions of typed nonterminals can only be set once." {
        let nont = nonterminal "N"
        nont.SetProductions(empty =% 0)
        // This call should be ignored. If not, building will fail.
        nont.SetProductions(empty =% 0, empty =% 1)
        nont
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> Flip.Expect.isOk "SetProductions can be set more than once."
    }

    test "The productions of untyped nonterminals can only be set once." {
        let nont = nonterminalU "N"
        nont.SetProductions(empty)
        // This call should be ignored. If not, building will fail.
        nont.SetProductions(empty, empty)
        nont
        |> DesigntimeFarkleBuild.createGrammarDefinition
        |> DesigntimeFarkleBuild.buildGrammarOnly
        |> Flip.Expect.isOk "SetProductions can be set more than once."
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

    test "Terminals named 'Newline' cannot terminate line groups" {
        let runtime =
            "X" |||= [!& "newline"; !& "x1" .>> "x2"]
            |> DesigntimeFarkle.addLineComment "//"
            |> RuntimeFarkle.buildUntyped
        let testString = "// newline\nx1 x2"

        let result = runtime.Parse testString

        Expect.isOk result "Parsing failed"
    }

    test "Farkle can properly handle block groups" {
        let runtime =
            Group.Block("Block Group", "{", "}", fun _ data -> data.ToString())
            |> RuntimeFarkle.build

        Expect.equal (runtime.Parse "{🆙🆙}") (Ok "{🆙🆙}") "Farkle does not properly handle block groups"
    }

    test "Renaming designtime Farkles works" {
        let doTest name (df: DesigntimeFarkle) =
            let expectedName = sprintf "%s Renamed" df.Name
            let (Nonterminal(_, startSymbolName)) =
                df.Rename(expectedName).BuildUntyped().GetGrammar().StartSymbol
            Expect.equal startSymbolName expectedName (sprintf "Renaming a %s had no effect" name)

        Terminals.int "Number"
        |> doTest "terminal"
        "Nonterminal" ||= [!@ (Terminals.int "Number") |> asIs]
        |> doTest "nonterminal"
        literal "Literal"
        |> doTest "literal"
        Group.Line("Line Group", "//")
        |> doTest "line group"
        Group.Block("Block Group", "/*", "*/")
        |> doTest "block group"
    }

    test "Newlines cannot be renamed" {
        let (Nonterminal(_, newlineRenamed)) =
            newline.Rename("NewLine Renamed").BuildUntyped().GetGrammar().StartSymbol
        Expect.equal newlineRenamed newline.Name "newline was renamed while it shouldn't"
    }

    test "Terminals named 'NewLine' will be automatically renamed when built" {
        let (Nonterminal(_, newlineName)) =
            Terminal.Literal(newline.Name).BuildUntyped().GetGrammar().StartSymbol
        Expect.notEqual newlineName newline.Name "The terminal was not renamed"
    }

    test "Typed designtime Farkles and nonterminals implement the IExposedAsDesigntimeFarkleChild interface" {
        let typesOfInterest = [typedefof<DesigntimeFarkle<_>>; typedefof<Nonterminal<_>>; typeof<Untyped.Nonterminal>]

        typeof<DesigntimeFarkle>.Assembly.GetTypes()
        |> Seq.filter (fun t -> typeof<DesigntimeFarkle>.IsAssignableFrom t)
        |> Seq.filter (fun t ->
            typesOfInterest
            |> List.exists (fun toi -> toi <> t && toi.IsAssignableFrom t))
        |> Flip.Expect.all
            "Not all required interfaces implement IExposedAsDesigntimeFarkleChild"
            (fun t -> typeof<IExposedAsDesigntimeFarkleChild>.IsAssignableFrom t)
    }

    test "Many block groups can be ended by the same symbol" {
        // It doesn't cause a DFA conflict because the
        // end symbols of the different groups are considered equal.
        let runtime =
            "Test" |||= [
                !% Group.Block("Group 1", "{", "}")
                !% Group.Block("Group 2", "[", "}")
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

    test "The many(1) operators work" {
        let mkRuntime atLeastOne =
            literal "x"
            |> DesigntimeFarkle.cast
            |> if atLeastOne then many1 else many
            |> RuntimeFarkle.buildUntyped
        let runtime = mkRuntime false
        let runtime1 = mkRuntime true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = String.replicate x "x"
            Expect.isOk (runtime.Parse s) (sprintf "Parsing %A with many failed" s)
            if x <> 0 then
                Expect.isOk (runtime1.Parse s) (sprintf "Parsing %A with many1 failed" s))
    }

    test "The sepBy(1) operators work" {
        let mkRuntime atLeastOne =
            literal "x"
            |> DesigntimeFarkle.cast
            |> (if atLeastOne then sepBy1 else sepBy) (literal ",")
            |> RuntimeFarkle.buildUntyped
        let runtime = mkRuntime false
        let runtime1 = mkRuntime true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = Seq.replicate x "x" |> String.concat ","
            Expect.isOk (runtime.Parse s) (sprintf "Parsing %A with sepBy failed" s)
            if x <> 0 then
                Expect.isOk (runtime1.Parse s) (sprintf "Parsing %A with sepBy1 failed" s))
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
                List.map (fun x -> !@ (mkTerminal x) |> asIs) testData
            |> DesigntimeFarkle.forceDynamicCodeGen
            |> RuntimeFarkle.build

        for x, _, _ in testData do
            Expect.equal (RuntimeFarkle.parseString runtime x) (Ok magic) (sprintf "%s was not parsed correctly" x)
    }

    test "Parser application errors are correctly handled" {
        let terminal =
            Regex.string "O"
            |> terminal "Terminal" (T(fun _ _ -> error "Terminal found" |> ignore))
        let designtime =
            "Nonterminal" ||= [!@ terminal |> asIs; empty => (fun () -> error "Empty input")]
        let runtime = designtime.Build()

        let mkError column msg =
            ParserError(Position.Create1 1 column, ParseErrorType.UserError msg)
            |> FarkleError.ParseError |> Error

        let error1 = mkError 8 "Terminal found"
        Expect.equal (runtime.Parse "       O") error1 "Application errors at transformers were not caught"

        let error2 = mkError 4 "Empty input"
        Expect.equal (runtime.Parse "   ") error2 "Application errors at fusers were not caught"
    }

    test "Syntax errors are reported in the right place" {
        let runtime =
            "X" |||= [!& "x1"; !& "x2"]
            |> RuntimeFarkle.buildUntyped
        let testString = "x1     x2"
        let expectedErrorPos = Position.Create1 1 8

        let error =
            runtime.Parse testString
            |> Flip.Expect.wantError "Parsing did not fail"

        match error with
        | FarkleError.ParseError (ParserError(actualErrorPos, _)) ->
            Expect.equal actualErrorPos expectedErrorPos "Parsing failed at a different position"
        | _ ->
            failtestf "Parsing failed with an unexpected error type: %A" error
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

    test "Farkle does not overflow the stack when processing a long designtime Farkle" {
        let length = 1000
        let df = "S" |||= [ProductionBuilder(Array.replicate length (box "x"))]

        let grammar =
            DesigntimeFarkleBuild.createGrammarDefinition df
            |> DesigntimeFarkleBuild.buildGrammarOnly

        Expect.isOk grammar "Building failed"
    }
]
