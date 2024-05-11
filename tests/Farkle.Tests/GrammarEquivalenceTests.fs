// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarEquivalenceTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammars
open Farkle.Grammars.StateMachines
open Farkle.Tests
open System.Collections.Generic
open System.Linq

// A major difference between Farkle and GOLD Parser is the way they number stuff.
// GOLD Parser numbers terminals based on the order they appear in the GML file.
// And LALR item sets are being created as they are traversed on a breadth-first search fashion.
// Farkle however takes a different approach. Because it uses regular objects to represent its grammars,
// it simply performs a depth-first search to discover symbols and item sets. That's the reason why
// the grammars generated by Farkle have different table indices from those generated by GOLD.
// And that's the reason we have to write this little function to check if the LALR states
// of a grammar are equivalent, up to the names of the states.
let checkLALRStateTableEquivalence (productionMap: Dictionary<_, _>) (farkleGrammar: Grammar) (goldGrammar: Grammar) =
    let lalrStates = Dictionary()
    let farkleStates = farkleGrammar.LrStateMachine
    Expect.isNotNull farkleStates "Farkle's grammar does not have an LALR state machine"
    let goldStates = goldGrammar.LrStateMachine
    Expect.isNotNull goldStates "GOLD Parser's grammar does not have an LALR state machine"

    let checkProductionEquivalence (pFarkle: ProductionHandle) (pGold: ProductionHandle) =
        if productionMap[pFarkle] <> pGold then
            failtestf "Farkle's grammar reduces production %O while GOLD Parser's grammar reduces production %O" pFarkle pGold

    let checkActionEquivalence sFarkle sGold =
        match sFarkle, sGold with
        | LrShift sFarkle, LrShift sGold ->
            match lalrStates.TryGetValue(sFarkle) with
            | true, sGoldExpected ->
                Expect.equal sGold sGoldExpected "There is a LALR state mismatch"
            | false, _ -> lalrStates.Add(sFarkle, sGold)
        | LrReduce pFarkle, LrReduce pGold ->
            checkProductionEquivalence pFarkle pGold
        | _, _ -> failtestf "Farkle's LALR action %O is not equivalent with GOLD Parser's %O" sFarkle sGold

    let checkEofActionEquivalence sFarkle sGold =
        match sFarkle, sGold with
        | LrEndOfFileReduce pFarkle, LrEndOfFileReduce pGold ->
            checkProductionEquivalence pFarkle pGold
        | LrEndOfFileAccept, LrEndOfFileAccept -> ()
        | _, _ -> failtestf "Farkle's LALR action %O is not equivalent with GOLD Parser's %O" sFarkle sGold

    let checkGotoEquivalence nont gFarkle gGold =
        match lalrStates.TryGetValue(gFarkle) with
        | true, gGoldExpected -> Expect.equal gGold gGoldExpected (sprintf "The GOTO actions for nonterminal %O are different" nont)
        | false, _ -> lalrStates.Add(gFarkle, gGold)

    lalrStates.Add(0, 0)
    Expect.hasLength farkleStates goldStates.Count "The grammars have a different number of LALR states"

    for i = 0 to farkleStates.Count - 1 do
        try
            let farkleState = farkleStates[i]
            let goldState = goldStates[lalrStates[i]]

            let getTerminalName (grammar: Grammar) (term: TokenSymbolHandle) =
                grammar.GetTokenSymbol(term).Name |> grammar.GetString

            Expect.hasLength farkleState.Actions goldState.Actions.Count "There are not the same number of LALR actions"
            let actionsJoined =
                farkleState.Actions.Join(
                    goldState.Actions,
                    (fun (KeyValue(term, _)) -> getTerminalName farkleGrammar term),
                    (fun (KeyValue(term, _)) -> getTerminalName goldGrammar term),
                    fun (KeyValue(_, farkleAction)) (KeyValue(_, goldAction)) -> farkleAction, goldAction)
            Expect.hasLength actionsJoined goldState.Actions.Count "Some terminals do not have a matching LALR action"
            for aFarkle, aGold in actionsJoined do
                checkActionEquivalence aFarkle aGold
                
            let getNonterminalName (grammar: Grammar) (nont: NonterminalHandle) =
                grammar.GetNonterminal(nont).Name |> grammar.GetString

            Expect.hasLength farkleState.Gotos goldState.Gotos.Count "There are not the same number of LALR GOTO actions"
            let gotoJoined =
                farkleState.Gotos.Join(
                    goldState.Gotos,
                    (fun (KeyValue(nont, _)) -> getNonterminalName farkleGrammar nont),
                    (fun (KeyValue(nont, _)) -> getNonterminalName goldGrammar nont),
                    fun (KeyValue(nont, farkleAction)) (KeyValue(_, goldAction)) -> nont, farkleAction, goldAction)
            Expect.hasLength gotoJoined goldState.Gotos.Count "Some nonterminals have no matching LALR GOTO action"
            for nont, gotoFarkle, gotoGold in gotoJoined do
                checkGotoEquivalence nont gotoFarkle gotoGold

            match Seq.tryHead farkleState.EndOfFileActions, Seq.tryHead goldState.EndOfFileActions with
            | Some aFarkle, Some aGold -> checkEofActionEquivalence aFarkle aGold
            | Some _, None -> failtest "GOLD Parser's grammar does not have an action on EOF, while Farkle's has"
            | None, Some _ -> failtest "Farkle's grammar does not have an action on EOF, while GOLD Parser's has"
            | None, None -> ()
        with
        | exn -> failtestf "Error in Farkle's state %d (GOLD Parser's state %d): %s" i lalrStates[i] exn.Message

let createProductionMap (farkleGrammar: Grammar) (goldGrammar: Grammar) =
    let farkleProductions = farkleGrammar.Productions
    let goldProductions = goldGrammar.Productions
    let dict = Dictionary()
    let goldProductions = HashSet(goldProductions)
    Expect.hasLength farkleProductions goldProductions.Count "The two grammars don't have the same number of productions"
    for farkleProduction in farkleProductions do
        let farkleProdHeadName = farkleGrammar.GetNonterminal(farkleProduction.Head).Name |> farkleGrammar.GetString
        goldProductions
        |> Seq.tryFind (fun goldProduction ->
            (goldGrammar.GetNonterminal(goldProduction.Head).Name |> goldGrammar.GetString) = farkleProdHeadName
            && string goldProduction = string farkleProduction)
        |> function
        | Some goldProduction ->
            dict.Add(farkleProduction.Handle, goldProduction.Handle)
            goldProductions.Remove(goldProduction) |> ignore
        | None -> failtestf "No matching GOLD Parser production was found for Farkle's %O" farkleProduction
    dict

let checkParserEquivalence (farkleGrammar: Grammar) (goldGrammar: Grammar) =
    let productionMap = createProductionMap farkleGrammar goldGrammar
    checkLALRStateTableEquivalence productionMap farkleGrammar goldGrammar

let recreateSyntaxFromGrammar (g: Grammar) =
    let terminals =
        g.Terminals
        |> Seq.map (fun t -> t.Name |> g.GetString |> virtualTerminal)
        |> Array.ofSeq
    let nonterminals =
        g.Nonterminals
        |> Seq.map (fun n -> n.Name |> g.GetString |> nonterminalU)
        |> Array.ofSeq
    let getSymbol (x: EntityHandle) =
        if x.IsTokenSymbol then
            terminals[TokenSymbolHandle.op_Explicit(x).Value]
        else
            nonterminals[NonterminalHandle.op_Explicit(x).Value]
    g.Nonterminals
    |> Seq.iteri (fun idx nont ->
        nont.Productions
        |> Seq.map (fun prod -> prod.Members |> Seq.fold (fun pb x -> pb .>> getSymbol x) empty)
        |> Array.ofSeq
        |> fun x -> nonterminals[idx].SetProductions(x))
    let parser =
        nonterminals[g.GrammarInfo.StartSymbol.Value]
            .AutoWhitespace(false)
            .WithGrammarName(g.GetString g.GrammarInfo.Name)
            .BuildSyntaxCheck()
    if parser.IsFailing then
        failtestf "Failed to build: %O" (parser.Parse "")
    parser.GetGrammar()

[<Tests>]
let farkleGOLDGrammarEquivalenceTests =
    ([
        "JSON", "JSON.egt"
        "the language of balanced parentheses", "balanced-parentheses.egt"
        "GOLD Meta-Language", "gml.egt"
        "COBOL-85", "COBOL85.egt"
    ]
    |> List.map (fun (name, egt) ->
        test (sprintf "Farkle and GOLD Parser generate an equivalent LALR parser for %s" name) {
            let gGold = loadGrammar egt
            let gFarkle = recreateSyntaxFromGrammar gGold
            checkParserEquivalence gFarkle gGold
        }
    ))
#if false // TODO-FARKLE7: This test very rarely runs because it requires GOLD Parser to be installed. Re-evaluate if it is needed.
    @ [
        testProperty "Farkle and GOLD build equivalent random grammars" (gen {
            GOLDParserBridge.checkIfGOLDExists()
            let! gDef = Gen.map DesigntimeFarkleBuild.createGrammarDefinition Arb.generate
            let farkleGrammar =
                gDef
                |> DesigntimeFarkleBuild.buildGrammarOnly
                |> Flip.Expect.wantOk "A faulty grammar was supposed to be filtered away"
            let goldGrammar = GOLDParserBridge.buildUsingGOLDParser gDef

            checkParserEquivalence farkleGrammar goldGrammar
        })
    ]
#endif
    |> testList "Farkle-GOLD grammar equivalence tests"
