// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarEquivalenceTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammar
open Farkle.PostProcessor
open Farkle.Tests
open SimpleMaths
open System.Collections.Generic
open System.Collections.Immutable

// A major difference between Farkle and GOLD Parser is the way they number stuff.
// GOLD Parser numbers terminals based on the order they appear in the GML file.
// And LALR item sets are being created as they are traversed on a breadth-first search fashion.
// Farkle however takes a different approach. Because it uses regular objects to represent its grammars,
// it simply performs a depth-first search to discover symbols and item sets. That's the reason why
// the grammars generated by Farkle have different table indices from those generated by GOLD.
// And that's the reason we have to write this little function to check if the LALR states
// of a grammar are equivalent, up to the names of the states.
let checkLALRStateTableEquivalence (productionMap: ImmutableDictionary<_, _>) (farkleStates: ImmutableArray<_>) (goldStates: ImmutableArray<_>) =
    let lalrStates = Dictionary()
    let checkProductionEquivalence pFarkle pGold =
        // That will trick the type inference
        pFarkle.Head = pGold.Head |> ignore
        if productionMap.[pFarkle.Index] <> pGold.Index then
            failtestf "Farkle's grammar reduces production %O while GOLD Parser's grammar reduces production %O" pFarkle pGold
    let checkActionEquivalence sFarkle sGold =
        match sFarkle, sGold with
        | LALRAction.Shift sFarkle, LALRAction.Shift sGold ->
            match lalrStates.TryGetValue(sFarkle) with
            | true, sGoldExpected ->
                Expect.equal sGold sGoldExpected "There is a LALR state mismatch"
            | false, _ -> lalrStates.Add(sFarkle, sGold)
        | LALRAction.Reduce pFarkle, LALRAction.Reduce pGold ->
            checkProductionEquivalence pFarkle pGold
        | LALRAction.Accept, LALRAction.Accept -> ()
        | _, _ -> failtestf "Farkle's LALR action %O is not equivalent with GOLD Parser's %O" sFarkle sGold
    let checkGotoEquivalence nont gFarkle gGold =
        match lalrStates.TryGetValue(gFarkle) with
        | true, gGoldExpected -> Expect.equal gGold gGoldExpected (sprintf "The GOTO actions for nonterminal %O are different" nont)
        | false, _ -> lalrStates.Add(gFarkle, gGold)
    lalrStates.Add(0u, 0u)
    Expect.hasLength farkleStates goldStates.Length "The grammars have a different number of LALR states"
    for i = 0 to farkleStates.Length - 1 do
        try
            let farkleState = farkleStates.[i]
            let goldState = goldStates.[int lalrStates.[uint32 i]]
            Expect.hasLength farkleState.Actions goldState.Actions.Count "There are not the same number of LALR actions"
            let actionsJoined = query {
                for aFarkle in farkleState.Actions do
                join aGold in goldState.Actions on (aFarkle.Key.Name = aGold.Key.Name)
                select (aFarkle.Value, aGold.Value)
            }
            Expect.hasLength actionsJoined goldState.Actions.Count "Some terminals do not have a matching LALR action"
            actionsJoined |> Seq.iter (fun (aFrakle, aGold) -> checkActionEquivalence aFrakle aGold)

            Expect.hasLength farkleState.GotoActions goldState.GotoActions.Count "There are not the same number of LALR GOTO actions"
            let gotoJoined = query {
                for gFarkle in farkleState.GotoActions do
                join gGold in goldState.GotoActions on (gFarkle.Key.Name = gGold.Key.Name)
                select (gFarkle.Key, gFarkle.Value, gGold.Value)
            }
            Expect.hasLength gotoJoined goldState.GotoActions.Count "Some nonterminals have no matching LALR GOTO action"
            gotoJoined |> Seq.iter (fun (nont, gFarkle, gGold) -> checkGotoEquivalence nont gFarkle gGold)

            match farkleState.EOFAction, goldState.EOFAction with
            | Some aFarkle, Some aGold -> checkActionEquivalence aFarkle aGold
            | Some _, None -> failtest "GOLD Parser's grammar does not have an action on EOF, while Farkle's has."
            | None, Some _ -> failtest "Farkle's grammar does not have an action on EOF, while GOLD Parser's has."
            | None, None -> ()
        with
        | exn -> failtestf "Error in Farkle's state %d (GOLD Parser's state %d): %s" i lalrStates.[uint32 i] exn.Message

let createProductionMap (farkleProductions: ImmutableArray<_>) (goldProductions: ImmutableArray<_>) =
    let dict = ImmutableDictionary.CreateBuilder()
    let goldProductions = HashSet(goldProductions)
    Expect.hasLength farkleProductions goldProductions.Count "The two grammars don't have the same number of productions."
    for i = 0 to farkleProductions.Length - 1 do
        let farkleProduction = farkleProductions.[i]
        goldProductions
        |> Seq.tryFind (fun p ->
            p.Head.Name = farkleProduction.Head.Name
            && string p = string farkleProduction)
        |> function
        | Some goldProduction ->
            dict.Add(farkleProduction.Index, goldProduction.Index)
            goldProductions.Remove(goldProduction) |> ignore
        | None -> failtestf "No matching GOLD Parser production was found for Farkle's %O." farkleProduction
    dict.ToImmutable()

let checkParserEquivalence (farkleProductions, farkleLALRStates) (goldProductions, goldLALRStates) =
    let productionMap = createProductionMap farkleProductions goldProductions
    checkLALRStateTableEquivalence productionMap farkleLALRStates goldLALRStates

let balancedParentheses =
    let expr = nonterminal "Expr"
    expr.SetProductions(
        !& "(" .>> expr .>> ")" .>> expr =% (),
        empty =% ()
    )
    RuntimeFarkle.buildUntyped expr

let rfIgnore x = RuntimeFarkle.changePostProcessor PostProcessor.syntaxCheck x

[<Tests>]
let farkleGOLDGrammarEquivalenceTests =
    [
        "the calculator", rfIgnore SimpleMaths.int, SimpleMaths.Definitions.Grammar.asBase64
        "the F# JSON parser", rfIgnore JSON.FSharp.Language.runtime, "./JSON.egt"
        "the C# JSON parser", rfIgnore JSON.CSharp.Language.Runtime, "./JSON.egt"
        "the language of balanced parentheses", balancedParentheses, "./balanced-parentheses.egt"
        "GOLD Meta-Language", rfIgnore GOLDMetaLanguage.runtime, "./gml.egt"
    ]
    |> List.map (fun (name, gFarkle, egt) ->
        test (sprintf "Farkle and GOLD Parser generate an equivalent LALR parser for %s" name) {
            let gFarkle = extractGrammar gFarkle
            let gGold =
                if egt.StartsWith("./") then
                    loadGrammar egt
                else
                    GOLDParser.EGT.ofBase64String egt
            checkParserEquivalence
                (gFarkle.Productions, gFarkle.LALRStates)
                (gGold.Productions, gGold.LALRStates)
        }
    )
    |> testList "Farkle-GOLD grammar equivalence tests"

[<Tests>]
let randomGrammarEquivalenceTest =
    testProperty "Random grammars generated by Farkle are equivalent to those generated by GOLD."
        (fun (FarkleVsGOLDParser(farkleGrammar, goldGrammar)) ->
            checkParserEquivalence
                (farkleGrammar.Productions, farkleGrammar.LALRStates)
                (goldGrammar.Productions, goldGrammar.LALRStates))
    |> List.singleton
    |> testList "Farkle-GOLD grammar equivalence tests"
