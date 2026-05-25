// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarBuilderTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Diagnostics
open Farkle.Grammars
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
        let parser = GrammarBuilder.buildSyntaxCheck symbol
        let result = CharParser.parseString parser ""

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
        let parser = Terminals.int64 "Signed" |> GrammarBuilder.build
        Expect.equal (parser.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing a signed integer failed")

    testProperty "Farkle can properly read unsigned integers" (fun num ->
        let parser = Terminals.uint64 "Unsigned" |> GrammarBuilder.build
        Expect.equal (parser.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing an unsigned integer failed")

    testProperty "Farkle can properly read floating-point numbers" (fun (NormalFloat num) ->
        let parser = Terminals.float "Floating-point" |> GrammarBuilder.build
        Expect.equal (parser.Parse(string num)) (ParserResult.CreateSuccess num) "Parsing an unsigned integer failed")

    test "Arithmetic overflows when parsing integers do not cause an exception" {
        // Add a space at the beginning to test position propagation.
        let testString = " 99999999999999999999"
        let doTest (builder: string -> IGrammarSymbol<_>) =
            let parser = (builder "Number").Build()
            Expect.isFalse (parser.IsFailing) "Building failed"
            let result = CharParser.parseString parser testString |> ParserResult.toResult
            let error = Expect.wantError result "Parsing should have failed"
            match error with
            | ParserDiagnostic(pos, :? string) -> Expect.equal pos.Column 2 "Parsing failed at a different position"
            | _ -> failwith "Parsing did not fail with a string"

        doTest Terminals.int
        doTest Terminals.int64
        doTest Terminals.uint32
        doTest Terminals.uint64
    }

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
        let parser =
            Group.Line("Line Group", "!!", fun _ data -> data.ToString())
            |> GrammarBuilder.build
        Expect.equal (parser.Parse "!! No new line") (ParserResult.CreateSuccess "!! No new line")
            "Farkle does not properly handle line groups that end on EOF"
        Expect.equal (parser.Parse "!! Has new line\n") (ParserResult.CreateSuccess "!! Has new line")
            "Farkle does not properly handle line groups that end on a new line"
    }

    test "Terminals named 'Newline' cannot terminate line groups" {
        let parser =
            "X" |||= [!& "newline"; !& "x1" .>> "x2"]
            |> _.AddLineComment("//")
            |> GrammarBuilder.buildSyntaxCheck
        let testString = "// newline\nx1 x2"

        let result = parser.Parse testString

        expectIsParseSuccess result "Parsing failed"
    }

    test "Farkle can properly handle block groups" {
        let parser =
            Group.Block("Block Group", "{", "}", fun _ data -> data.ToString())
            |> GrammarBuilder.build

        Expect.equal (parser.Parse "{🆙🆙}") (ParserResult.CreateSuccess "{🆙🆙}") "Farkle does not properly handle block groups"
    }

    test "Farkle can properly handle recursive block groups" {
        let parser =
            Group.Block("Block Group", "{", "}", (fun _ data -> data.ToString()), GroupOptions.Recursive)
            |> GrammarBuilder.build

        Expect.equal (parser.Parse "{{🆙🆙}}") (ParserResult.CreateSuccess "{{🆙🆙}}") "Farkle does not properly handle recursive block groups"
    }

    test "Special names work" {
        // Test setting special names from multiple instances of the same symbol.
        let sym = virtualTerminal "MyTerminal"
        let sym2 = sym.AddSpecialName("__MySpecialName").AddSpecialName("__MySpecialName2")
        let sym3 = sym2.AddSpecialName("__MySpecialName2").AddSpecialName("__MySpecialName3")
        let nont = "N" |||= [
            !% sym .>> sym2 .>> sym3
        ]
        let grammar, warnings =
            nont.AddSpecialName("__MySpecialName4")
            |> _.AutoWhitespace(false)
            |> buildWithWarnings
        let terminal =
            grammar.Terminals
            |> Seq.exactlyOne
        let nonterminal =
            grammar.Nonterminals
            |> Seq.exactlyOne
        Expect.isEmpty warnings "Building emitted warnings"
        Expect.hasLength grammar.SpecialNameDefinitions 4 "The grammar does not have the right number of special name definitions"
        Expect.equal (grammar.GetTokenSymbolFromSpecialName "__MySpecialName") terminal.Handle "The terminal could not be retrieved from the special name."
        Expect.equal (grammar.GetTokenSymbolFromSpecialName "__MySpecialName2") terminal.Handle "The terminal could not be retrieved from the special name."
        Expect.equal (grammar.GetTokenSymbolFromSpecialName "__MySpecialName3") terminal.Handle "The terminal could not be retrieved from the special name."
        Expect.equal (grammar.GetNonterminalFromSpecialName "__MySpecialName4") nonterminal.Handle "The nonterminal could not be retrieved from the special name."
    }

    test "Special names on literals work" {
        let sym1 = literal "a" |> _.AddSpecialName("__MySpecialName")
        let sym2 = literal "A" |> _.AddSpecialName("__MySpecialName")
        let sym = "N" |||= [
            !% sym1 .>> sym2
        ]
        let grammar, warnings =
            sym
            |> _.AutoWhitespace(false)
            |> _.CaseSensitive(false)
            |> buildWithWarnings
        let terminal =
            grammar.Terminals
            |> Seq.exactlyOne
        Expect.isEmpty warnings "Building emitted warnings"
        Expect.hasLength grammar.SpecialNameDefinitions 1 "The grammar does not have the right number of special name definitions"
        let specialNameDef =
            grammar.SpecialNameDefinitions
            |> Seq.exactlyOne
        Expect.equal specialNameDef.Name "__MySpecialName" "The special name was not set correctly."
        Expect.equal specialNameDef.Symbol (SymbolDefinition terminal) "The special name was not set to the correct symbol."
        Expect.equal (grammar.GetTokenSymbolFromSpecialName "__MySpecialName") terminal.Handle "The terminal could not be retrieved from the special name."
    }

    test "Duplicate special names emit an error" {
        let sym = virtualTerminal "Test" |> _.AddSpecialName("__MySpecialName")
        let sym2 = virtualTerminal "Test 2" |> _.AddSpecialName("__MySpecialName")
        let nont = "N" |||= [
            !% sym
            !% sym2
        ]
        let grammar, warnings =
            nont.AutoWhitespace false
            |> buildWithWarnings
        Expect.hasLength warnings 1 "Building emitted the wrong number of warnings"
        Expect.equal warnings[0].Code "FARKLE0004" "The warning was not of the correct type"
        Expect.isEmpty grammar.SpecialNameDefinitions "The grammar should not have any special name definitions"
        Expect.equal grammar.GrammarInfo.Attributes GrammarAttributes.Unparsable "The grammar was not marked as unparsable"
        Expect.isFalse (grammar.GetSymbolFromSpecialName("__MySpecialName").HasValue) "The special name should not be present in the grammar file"
    }

    test "An invalid string regex causes no DFA to be built" {
        let grammar, errors =
            Regex.regexString "("
            |> terminalU "T"
            |> buildWithWarnings
        Expect.isNull grammar.DfaOnChar "The DFA should not have been built"
        Expect.hasLength errors 1 "Building emitted the wrong number of errors"
        Expect.equal errors[0].Code "FARKLE0008" "The error was not of the correct type"
    }

    test "A deeply nested regex does not cause a stack overflow" {
        let depth = 10_000
        let regex =
            Seq.replicate depth (Regex.string "a")
            // acc + x would flatten the tree, but concat does not.
            |> Seq.fold (fun acc x -> Regex.concat [acc; x]) (Regex.string "")
        let grammar, errors =
            regex
            |> terminalU "T"
            |> buildWithWarnings
        Expect.isNull grammar.DfaOnChar "The DFA should not have been built"
        Expect.hasLength errors 1 "Building emitted the wrong number of errors"
        Expect.equal errors[0].Code "FARKLE0009" "The error was not of the correct type"
    }

    test "Many block groups can be ended by the same symbol" {
        // It doesn't cause a DFA conflict because the
        // end symbols of the different groups are considered equal.
        let parser =
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
        |> List.iter (fun x -> expectIsParseSuccess (parser.Parse x) (sprintf "Parsing %s failed" x))
    }

    test "Parsing untyped groups works" {
        let parser =
            "Test" ||= [
                !% Group.Block("Untyped Group", "{", "}") =% ()
            ]
            |> GrammarBuilder.build

        expectIsParseSuccess (parser.Parse "{test}") "Parsing a test string failed"
    }

    test "The many(1) operators work" {
        let mkParser atLeastOne =
            literal "x"
            |> _.Cast()
            |> if atLeastOne then many1 else many
            |> GrammarBuilder.buildSyntaxCheck
        let parser = mkParser false
        let parser1 = mkParser true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = String.replicate x "x"
            expectIsParseSuccess (parser.Parse s) (sprintf "Parsing %A with many failed" s)
            if x <> 0 then
                expectIsParseSuccess (parser1.Parse s) (sprintf "Parsing %A with many1 failed" s))
    }

    test "The sepBy(1) operators work" {
        let mkParser atLeastOne =
            literal "x"
            |> _.Cast()
            |> (if atLeastOne then sepBy1 else sepBy) (literal ",")
            |> GrammarBuilder.buildSyntaxCheck
        let parser = mkParser false
        let parser1 = mkParser true

        [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 100]
        |> List.iter (fun x ->
            let s = Seq.replicate x "x" |> String.concat ","
            expectIsParseSuccess (parser.Parse s) (sprintf "Parsing %A with sepBy failed" s)
            if x <> 0 then
                expectIsParseSuccess (parser1.Parse s) (sprintf "Parsing %A with sepBy1 failed" s))
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
        let parser =
            "Test" ||=
                List.map (fun x -> !@ (mkTerminal x) |> asProduction) testData
            |> DesigntimeFarkle.forceDynamicCodeGen
            |> GrammarBuilder.build

        for x, _, _ in testData do
            Expect.equal (CharParser.parseString parser x) (ParserResult.CreateSuccess magic) (sprintf "%s was not parsed correctly" x)
    }
#endif

    test "Parser application errors are correctly handled" {
        let terminal =
            Regex.string "O"
            |> terminal "Terminal" (T(fun _ data -> errorf "Terminal found: %s" (data.ToString()) |> ignore))
        let grammar =
            "Nonterminal" ||= [!@ terminal |> asProduction; empty => (fun () -> error "Empty input")]
        let parser = grammar.Build()

        let doTest input column expectedError assertMsg =
            match CharParser.parseString parser input with
            | ParserError(ParserDiagnostic(pos, error)) ->
                Expect.equal pos (TextPosition.Create1(1, column)) "Parsing failed at the wrong position"
                Expect.equal error expectedError assertMsg
            | x -> failtestf "Parsing did not fail with an error: %A" x

        doTest "       O" 8 "Terminal found: O" "Application errors at transformers were not caught"
        doTest "   " 4 "Empty input" "Application errors at fusers were not caught"
    }

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
