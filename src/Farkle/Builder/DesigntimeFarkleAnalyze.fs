// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Builder.OperatorPrecedence
open Farkle.Common
open System.Collections.Generic
open System.Collections.Immutable
open System.Diagnostics
open System.Threading

[<Struct>]
// Gives a name to an object.
// Because designtime Farkles may have their names changed
// when unwrapped, this type preserves their intended names.
type internal Named<'T> = Named of name: string * 'T

[<RequireQualifiedAccess>]
// A strongly-typed representation of all kinds of
// designtime Farkles that will lead to terminals.
type internal TerminalEquivalent =
    | Terminal of AbstractTerminal
    | Literal of string
    | NewLine
    | LineGroup of AbstractLineGroup
    | BlockGroup of AbstractBlockGroup
    | VirtualTerminal of VirtualTerminal

[<NoComparison; ReferenceEquality>]
// The contents of a designtime Farkle in a least processed form.
// This type must not contain any types from the Farkle.Grammar namespace.
// Why does this type exist? Good question. In the past, DesigntimeFarkleBuild
// was a big and complex module, partly because it was doing two things at
// the same time: traverse the graph of designtime Farkles, and convert
// the builder types to grammar files. From now on, code in this type bothers
// only with the former, and DFB only with the latter. And this has another advantage,
// creating post-processors is cheaper because less unnecessary things are done,
// which is important for the startup speed of precompiled grammars, for which
// stuff like the conflict resolver can also be trimmed.
type internal DesigntimeFarkleDefinition = {
    Metadata: GrammarMetadata
    TerminalEquivalents: TerminalEquivalent Named ResizeArray
    // The first nonterminal is the starting one.
    Nonterminals: AbstractNonterminal Named ResizeArray
    Productions: AbstractProduction ResizeArray
    OperatorScopes: OperatorScope HashSet
}

[<CompiledName("DesigntimeFarkleAnalyze")>]
module internal DesigntimeFarkle =

    // These two types are used when a designtime Farkle made of only one terminal
    // (say x) is going to be built. They create a grammar with a start symbol S -> x.
    type private PlaceholderProduction(df) =
        static let fuserDataPickFirst = FuserData.CreateAsIs 0
        let members = ImmutableArray.Create(DesigntimeFarkle.unwrap df)
        interface AbstractProduction with
            member _.ContextualPrecedenceToken = null
            member _.Fuser = fuserDataPickFirst
            member _.Members = members
    type private PlaceholderNonterminal(df: DesigntimeFarkle) =
        let name = df.Name
        let prod = PlaceholderProduction df :> AbstractProduction
        let productions = [prod]
        member _.SingleProduction = prod
        interface DesigntimeFarkle with
            member _.Name = name
            member _.Metadata = GrammarMetadata.Default
        interface AbstractNonterminal with
            member _.Freeze() = ()
            member _.Productions = productions

    let rec private addOperatorScope (set: HashSet<_>) (df: DesigntimeFarkle) =
        match df with
        | :? DesigntimeFarkleWithOperatorScope as dfog ->
            set.Add(dfog.OperatorScope) |> ignore
        | :? DesigntimeFarkleWrapper as dfw ->
            addOperatorScope set dfw.InnerDesigntimeFarkle
        | _ -> ()

    let analyze (ct: CancellationToken) (df: DesigntimeFarkle) =
        let terminalEquivalents = ResizeArray()
        let nonterminals = ResizeArray()
        let productions = ResizeArray()
        let operatorScopes = HashSet()

        let visited = HashSet(FallbackStringComparers.get df.Metadata.CaseSensitive)
        let nonterminalsToProcess = Queue()

        let visit (df: DesigntimeFarkle) =
            let name = df.Name
            match DesigntimeFarkle.unwrap df with
            | :? AbstractNonterminal as nont ->
                if visited.Add nont then
                    addOperatorScope operatorScopes df
                    nont.Freeze()
                    nonterminals.Add(Named(name, nont))
                    nonterminalsToProcess.Enqueue(nont)
            | dfUnwrapped ->
                let isFirstTimeVisit =
                    match dfUnwrapped with
                    | :? Literal as lit -> box lit.Content
                    | _ -> box dfUnwrapped
                    |> visited.Add
                if isFirstTimeVisit then
                    addOperatorScope operatorScopes df
                    let te =
                        match dfUnwrapped with
                        | :? AbstractTerminal as term -> TerminalEquivalent.Terminal term
                        | :? Literal as lit -> TerminalEquivalent.Literal lit.Content
                        | :? NewLine -> TerminalEquivalent.NewLine
                        | :? AbstractLineGroup as lg -> TerminalEquivalent.LineGroup lg
                        | :? AbstractBlockGroup as bg -> TerminalEquivalent.BlockGroup bg
                        | :? VirtualTerminal as vt -> TerminalEquivalent.VirtualTerminal vt
                        | _ -> invalidOp "Using a custom implementation of the DesigntimeFarkle interface is not allowed."
                    terminalEquivalents.Add(Named(name, te))

        visit df
        while nonterminalsToProcess.Count <> 0 do
            ct.ThrowIfCancellationRequested()
            let nont = nonterminalsToProcess.Dequeue()
            for prod in nont.Productions do
                productions.Add prod
                for x in prod.Members do visit x

        if nonterminals.Count = 0 then
            Debug.Assert(terminalEquivalents.Count = 1 && productions.Count = 0)
            let nont = PlaceholderNonterminal df
            nonterminals.Add(Named(df.Name, nont :> _))
            productions.Add nont.SingleProduction

        {
            Metadata = df.Metadata
            TerminalEquivalents = terminalEquivalents
            Nonterminals = nonterminals
            Productions = productions
            OperatorScopes = operatorScopes
        }
