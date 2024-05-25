// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarBuilderTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Parser
open FsCheck
open System

type TestClass = TestClass of string
with
    member x.ClassInstance(_: ParserState byref, _: ReadOnlySpan<char>) =
        match x with TestClass x -> x
    static member ClassStatic (TestClass x, _: ParserState byref, _: ReadOnlySpan<char>) = x

[<Struct>]
type TestStruct = TestStruct of string
with
    member x.StructInstance(_: ParserState byref, _: ReadOnlySpan<char>) =
        match x with TestStruct x -> x

[<Tests>]
let tests = testList "Grammar builder tests" [
    test "Duplicate literals do not give an error" {
        let nt = "Colliding" ||= [
            !% (literal "a") =% 1
            !% (literal "a") .>> literal "b" =% 2
        ]
        Expect.isFalse (nt.Build().IsFailing) "Duplicate literals give an error"
    }

    test "A grammar that only accepts the empty string indeed accepts it" {
        let symbol = "S" |||= [empty]
        let runtime = GrammarBuilder.buildSyntaxCheck symbol
        let result = CharParser.parseString runtime ""

        expectIsParseSuccess result "Something went wrong"
    }

    test "A grammar with a nullable terminal is not accepted" {
        let symbol =
            "S" |||= [!% (terminalU "Nullable" (Regex.chars "123" |> Regex.atLeast 0))]
        Expect.isTrue (symbol.BuildSyntaxCheck().IsFailing) "A grammar with a nullable terminal was accepted"
    }

    test "IGrammarSymbol objects have reference equality semantics" {
        let lit1 = literal "Test"
        let lit2 = literal "Test"
        Expect.isFalse (lit1 = lit2) "Literals are not checked for reference equality"

        let t1 = terminal "Test" (T(fun _ _ -> null)) (Regex.string "Test")
        let t2 = terminal "Test" (T(fun _ _ -> null)) (Regex.string "Test")
        Expect.isFalse (t1 = t2) "Terminals are not checked for reference equality"

        let nont1 = nonterminal "Test" :> IGrammarSymbol
        let nont2 = nonterminal "Test" :> IGrammarSymbol
        Expect.isFalse (nont1 = nont2) "Nonterminals are not checked for reference equality"
    }

    testProperty "Farkle can properly read signed integers" (fun num ->
        let runtime = Terminals.int64 "Signed" |> GrammarBuilder.build
        Expect.equal (runtime.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing a signed integer failed")

    testProperty "Farkle can properly read unsigned integers" (fun num ->
        let runtime = Terminals.uint64 "Unsigned" |> GrammarBuilder.build
        Expect.equal (runtime.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing an unsigned integer failed")

    testProperty "Farkle can properly read floating-point numbers" (fun (NormalFloat num) ->
        let runtime = Terminals.float "Floating-point" |> GrammarBuilder.build
        Expect.equal (runtime.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing an unsigned integer failed")

    test "IGrammarSymbols, productions, and transformers are covariant" {
        let symbol = terminal "x" (T(fun _ _ -> "")) (Regex.string "x")
        let prod = !& "x" =% ""
        let t = T<char,_>(fun _ x -> x.ToString())
        let tInt = T<char,_>(fun _ _ -> 380)
        Expect.isSome (tryUnbox<IGrammarSymbol<obj>> symbol) "Symbols are not covariant"
        Expect.isSome (tryUnbox<IProduction<obj>> prod) "Productions are not covariant"
        Expect.isSome (tryUnbox<T<char, obj>> t) "Transformers are not covariant"
        Expect.isNone (tryUnbox<T<char, obj>> tInt) "Transformers on value types are covariant while they shouldn't"
    }

    test "The productions of typed nonterminals can only be set once." {
        let nont = nonterminal "N"
        nont.SetProductions(empty =% 0)
        Expect.throws (fun () -> nont.SetProductions(empty =% 0, empty =% 1)) "SetProductions can be set more than once."
    }

    test "The productions of untyped nonterminals can only be set once." {
        let nont = nonterminalU "N"
        nont.SetProductions(empty)
        Expect.throws (fun () -> nont.SetProductions(empty, empty)) "SetProductions can be set more than once."
    }

    test "Farkle can properly handle line groups" {
        let runtime =
            Group.Line("Line Group", "!!", fun _ data -> data.ToString())
            |> GrammarBuilder.build
        Expect.equal (runtime.Parse "!! No new line") (ParserResult.CreateSuccess "!! No new line")
            "Farkle does not properly handle line groups that end on EOF"
        Expect.equal (runtime.Parse "!! Has new line\n") (ParserResult.CreateSuccess "!! Has new line")
            "Farkle does not properly handle line groups that end on a new line"
    }

    test "Terminals named 'Newline' cannot terminate line groups" {
        let runtime =
            "X" |||= [!& "newline"; !& "x1" .>> "x2"]
            |> _.AddLineComment("//")
            |> GrammarBuilder.buildSyntaxCheck
        let testString = "// newline\nx1 x2"

        let result = runtime.Parse testString

        expectIsParseSuccess result "Parsing failed"
    }

    test "Farkle can properly handle block groups" {
        let runtime =
            Group.Block("Block Group", "{", "}", fun _ data -> data.ToString())
            |> GrammarBuilder.build

        Expect.equal (runtime.Parse "{🆙🆙}") (ParserResult.CreateSuccess "{🆙🆙}") "Farkle does not properly handle block groups"
    }

    test "Farkle can properly handle recursive block groups" {
        let runtime =
            Group.Block("Block Group", "{", "}", (fun _ data -> data.ToString()), GroupOptions.Recursive)
            |> GrammarBuilder.build

        Expect.equal (runtime.Parse "{{🆙🆙}}") (ParserResult.CreateSuccess "{{🆙🆙}}") "Farkle does not properly handle recursive block groups"
    }

    test "Renaming grammar symbols works" {
        let doTest name (df: IGrammarSymbol) =
            let expectedName = sprintf "%s Renamed" df.Name
            let startSymbolName =
                let grammar = df.Rename(expectedName).BuildSyntaxCheck().GetGrammar()
                grammar.GetNonterminal(grammar.GrammarInfo.StartSymbol).Name |> grammar.GetString
            Expect.equal startSymbolName expectedName (sprintf "Renaming a %s had no effect" name)

        terminalU "Number" Regex.any
        |> doTest "terminal"
        "Nonterminal" |||= [!% (terminalU "Number" Regex.any)]
        |> doTest "nonterminal"
        literal "Literal"
        |> doTest "literal"
        Group.Line("Line Group", "//")
        |> doTest "line group"
        Group.Block("Block Group", "/*", "*/")
        |> doTest "block group"
    }

    test "The renamed name of a symbol gets preferred" {
        let sym = virtualTerminal "Test"
        // Test that multiple symbol objects with the same renamed name are accepted.
        let mkRenamed() = GrammarSymbol.renameU "Test Renamed" sym
        let nont = "N" |||= [
            // While traversing the grammar, the builder will see the original symbol first,
            // but must still pick the renamed one.
            !% sym
            !% mkRenamed() .>> mkRenamed()
        ]
        let grammar, warnings =
            nont.AutoWhitespace(false)
            |> buildWithWarnings
        Expect.isEmpty warnings "Building emitted warnings"
        let terminalName =
            grammar.Terminals
            |> Seq.exactlyOne
            |> _.Name
            |> grammar.GetString
        Expect.equal terminalName "Test Renamed" "The renamed name of the symbol was not preferred"
    }

    test "Renaming a symbol twice raises a warning" {
        let sym = virtualTerminal "Test"
        let mkRenamed() = GrammarSymbol.renameU "Test Renamed" sym
        let mkRenamed2() = GrammarSymbol.renameU "Test Renamed 2" sym
        let nont = "N" |||= [
            !% sym
            !% mkRenamed() .>> mkRenamed()
            !% mkRenamed2() .>> mkRenamed2() .>> mkRenamed2()
        ]
        let grammar, warnings =
            nont.AutoWhitespace(false)
            |> buildWithWarnings
        Expect.hasLength warnings 3 "Building emitted the wrong number of warnings"
        Expect.all warnings (fun x -> x.Code = "FARKLE0008") "Warnings were not of the correct type"
        let terminalName =
            grammar.Terminals
            |> Seq.exactlyOne
            |> _.Name
            |> grammar.GetString
        // We can't know for sure which name will be chosen, but it will not be the original one.
        Expect.notEqual terminalName "Test" "The original name of a symbol was not preferred"
    }

    test "Special names work" {
        let sym = Terminal.Virtual("__MySpecialName", TerminalOptions.SpecialName).Rename("MyTerminal")
        let nont = "N" |||= [
            !% sym
        ]
        let grammar, warnings =
            nont.AutoWhitespace(false)
            |> buildWithWarnings
        let terminal =
            grammar.Terminals
            |> Seq.exactlyOne
        let terminalFromSpecialName =
            grammar.GetSymbolFromSpecialName("__MySpecialName")
            |> Grammars.TokenSymbolHandle.op_Explicit
        Expect.isEmpty warnings "Building emitted warnings"
        Expect.equal terminalFromSpecialName terminal.Handle "The terminal could not be retrieved from the special name."
    }

    test "Duplicate special names emit an error" {
        let sym = Terminal.Virtual("__MySpecialName", TerminalOptions.SpecialName).Rename("Test")
        let sym2 = Terminal.Virtual("__MySpecialName", TerminalOptions.SpecialName).Rename("Test 2")
        let nont = "N" |||= [
            !% sym
            !% sym2
        ]
        let grammar, warnings =
            nont.AutoWhitespace(false)
            |> buildWithWarnings
        Expect.hasLength warnings 1 "Building emitted the wrong number of warnings"
        Expect.equal warnings.[0].Code "FARKLE0004" "The warning was not of the correct type"
        Expect.equal grammar.GrammarInfo.Attributes Grammars.GrammarAttributes.Unparsable "The grammar was not marked as unparsable"
        Expect.isFalse (grammar.GetSymbolFromSpecialName("__MySpecialName").HasValue) "The special name should not be present in the grammar file"
    }

    test "Many block groups can be ended by the same symbol" {
        // It doesn't cause a DFA conflict because the
        // end symbols of the different groups are considered equal.
        let runtime =
            "Test" |||= [
                !% Group.Block("Group 1", "{", "}")
                !% Group.Block("Group 2", "[", "}")
                // Test conflict between group end and literal.
                // Conflicts with regular terminals cannot be resolved yet.
                // Once we implement #153, we will have a separate DFA for
                // inside each group, and the regular terminal will be out
                // of the picture.
                !% Group.Block("Group 3", "(", ")")
                !& ")"
            ]
            |> GrammarBuilder.buildSyntaxCheck

        ["{}"; "[}"; "()"; ")"]
        |> List.iter (fun x -> expectIsParseSuccess (runtime.Parse x) (sprintf "Parsing %s failed" x))
    }

    test "Parsing untyped groups works" {
        let runtime =
            "Test" ||= [
                !% Group.Block("Untyped Group", "{", "}") =% ()
            ]
            |> GrammarBuilder.build

        expectIsParseSuccess (runtime.Parse "{test}") "Parsing a test string failed"
    }

    test "The many(1) operators work" {
        let mkRuntime atLeastOne =
            literal "x"
            |> _.Cast()
            |> if atLeastOne then many1 else many
            |> GrammarBuilder.buildSyntaxCheck
        let runtime = mkRuntime false
        let runtime1 = mkRuntime true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = String.replicate x "x"
            expectIsParseSuccess (runtime.Parse s) (sprintf "Parsing %A with many failed" s)
            if x <> 0 then
                expectIsParseSuccess (runtime1.Parse s) (sprintf "Parsing %A with many1 failed" s))
    }

    test "The sepBy(1) operators work" {
        let mkRuntime atLeastOne =
            literal "x"
            |> _.Cast()
            |> (if atLeastOne then sepBy1 else sepBy) (literal ",")
            |> GrammarBuilder.buildSyntaxCheck
        let runtime = mkRuntime false
        let runtime1 = mkRuntime true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = Seq.replicate x "x" |> String.concat ","
            expectIsParseSuccess (runtime.Parse s) (sprintf "Parsing %A with sepBy failed" s)
            if x <> 0 then
                expectIsParseSuccess (runtime1.Parse s) (sprintf "Parsing %A with sepBy1 failed" s))
    }

#if false // TODO-FARKLE7: Reevaluate when codegen is implemented in Farkle 7.
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
            let t = typ.GetMethod(name).CreateDelegate<T<char,string>>(target)
            terminal name t (Regex.string name)
        let runtime =
            "Test" ||=
                List.map (fun x -> !@ (mkTerminal x) |> asProduction) testData
            |> DesigntimeFarkle.forceDynamicCodeGen
            |> GrammarBuilder.build

        for x, _, _ in testData do
            Expect.equal (CharParser.parseString runtime x) (ParserResult.CreateSuccess magic) (sprintf "%s was not parsed correctly" x)
    }
#endif

#if false // TODO-FARKLE7: Reevaluate when user errors are implemented in Farkle 7.
    test "Parser application errors are correctly handled" {
        let terminal =
            Regex.string "O"
            |> terminal "Terminal" (T(fun _ _ -> error "Terminal found" |> ignore))
        let designtime =
            "Nonterminal" ||= [!@ terminal |> asProduction; empty => (fun () -> error "Empty input")]
        let runtime = designtime.Build()

        let mkError column msg =
            ParserError(Position.Create1 1 column, ParseErrorType.UserError msg)
            |> FarkleError.ParseError |> Error

        let error1 = mkError 8 "Terminal found"
        Expect.equal (runtime.Parse "       O") error1 "Application errors at transformers were not caught"

        let error2 = mkError 4 "Empty input"
        Expect.equal (runtime.Parse "   ") error2 "Application errors at fusers were not caught"
    }
#endif

    test "Farkle does not overflow the stack when processing a deep grammar symbol" {
        let depth = 1000
        let nonterminals = Array.init depth (sprintf "N%d" >> nonterminalU)

        for i = 0 to nonterminals.Length - 2 do
            nonterminals.[i].SetProductions(!% nonterminals.[i + 1])
        nonterminals.[nonterminals.Length - 1].SetProductions(!& "x")

        let grammar =
            GrammarBuilder.buildSyntaxCheck nonterminals[0]
        Expect.isFalse grammar.IsFailing "Building failed"
        expectIsParseSuccess (grammar.Parse "x") "Parsing failed"
    }

    test "Farkle does not overflow the stack when processing a long grammar symbol" {
        let length = 1000
        let nonterminal = Nonterminal.CreateUntyped("S", ProductionBuilder(Array.replicate length (box "x")))

        let grammar =
            GrammarBuilder.buildSyntaxCheck nonterminal
        Expect.isFalse grammar.IsFailing "Building failed"
    }
]
