// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Builder.OperatorPrecedence
open Farkle.Common
open Farkle.Grammars
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
    | Terminal of ITerminal
    | Literal of string
    | NewLine
    | LineGroup of ILineGroup
    | BlockGroup of IBlockGroup
    | VirtualTerminal of VirtualTerminal
    member x.CreateNamed(name) =
        let namePatched =
            match x with
            | NewLine -> Terminal.NewLineName
            // In Farkle's grammar domain model, groups can end by either a group end symbol,
            // or a newline. Newlines are considered the terminals that are case-insensitively
            // named "NewLine". However, another such terminal can exist and can cause unexplained
            // errors when put inside a line group. This problem does not exist in GOLD Parser,
            // nor did in Farkle's first grammar domain model, where the terminal that ended a
            // group was explicitly specified. Prepending an underscore to the names of terminals
            // that could be misrepresented as newlines is the least breaking fix, until the
            // domain model gets overhauled in the next major version.
            // TODO: Remove this temporary workaround.
            | _ when Terminal.IsNamedNewLine name -> "_" + name
            | _ -> name
        Named(namePatched, x)
    // An object representing this terminal equivalent for equality comparisons.
    member x.IdentityObject =
        match x with
        | Terminal term -> term.IdentityObject
        | Literal lit -> box lit
        | NewLine -> box Terminal.NewLine
        | LineGroup lg -> box lg
        | BlockGroup bg -> box bg
        | VirtualTerminal vt -> box vt

[<NoComparison; ReferenceEquality>]
// The contents of a designtime Farkle in a least processed form.
// This type must not contain any types from the Farkle.Grammars namespace.
// Why does this type exist? Good question. In the past, DesigntimeFarkleBuild
// was a big and complex module, partly because it was doing two things at
// the same time: traverse the graph of designtime Farkles, and convert
// the builder types to grammar types. From now on, code in this type bothers
// only with the former, and DFB only with the latter. And this has another advantage,
// creating post-processors is cheaper because less unnecessary things are done,
// which is important for the startup speed of precompiled grammars, for which
// stuff like the conflict resolver can also be trimmed.
type internal DesigntimeFarkleDefinition = {
    Metadata: GrammarMetadata
    TerminalEquivalents: TerminalEquivalent Named ResizeArray
    // The first nonterminal is the starting one.
    Nonterminals: INonterminal Named ResizeArray
    Productions: (INonterminal * IProduction) ResizeArray
    OperatorScopes: OperatorScope HashSet
}

module internal DesigntimeFarkleAnalyze =

    // These two types are used when a designtime Farkle made of only one terminal
    // (say x) is going to be built. They create a grammar with a start symbol S -> x.
    type private PlaceholderProduction(df) =
        static let fuserDataPickFirst = FuserData.CreateAsIs 0
        let members = ImmutableArray.Create(DesigntimeFarkle.unwrap df)
        interface IProduction with
            member _.ContextualPrecedenceToken = null
            member _.Fuser = fuserDataPickFirst
            member _.Members = members
    type private PlaceholderNonterminal(df) =
        let prod = PlaceholderProduction df :> IProduction
        let productions = [prod]
        member _.SingleProduction = prod
        interface DesigntimeFarkle with
            member _.Name = df.Name
        interface INonterminal with
            member _.FreezeAndGetProductions() = productions

    let analyze (ct: CancellationToken) (df: DesigntimeFarkle) =
        let metadata = DesigntimeFarkle.getMetadata df
        let terminalEquivalents = ResizeArray()
        let nonterminals = ResizeArray()
        let productions = ResizeArray()
        let operatorScopes = HashSet()

        let visited = HashSet(FallbackStringComparers.get metadata.CaseSensitive)
        let nonterminalsToProcess = Queue()

        let considerLocalMetadata (df: DesigntimeFarkle) =
            match df with
            | :? DesigntimeFarkleWrapper as dfw ->
                let scope = dfw.Metadata.OperatorScope
                if scope <> OperatorScope.Empty && not (obj.ReferenceEquals(scope, null)) then
                    operatorScopes.Add(scope) |> ignore
            | _ -> ()

        let visit (df: DesigntimeFarkle) =
            let name = df.Name
            match DesigntimeFarkle.unwrap df with
            | :? INonterminal as nont ->
                if visited.Add nont then
                    considerLocalMetadata df
                    nonterminals.Add(Named(name, nont))
                    nonterminalsToProcess.Enqueue(nont)
            | dfUnwrapped ->
                if visited.Add (DesigntimeFarkle.getIdentityObject dfUnwrapped) then
                    considerLocalMetadata df
                    let te =
                        match dfUnwrapped with
                        | :? ITerminal as term -> TerminalEquivalent.Terminal term
                        | :? Literal as lit -> TerminalEquivalent.Literal lit.Content
                        | :? NewLine -> TerminalEquivalent.NewLine
                        | :? ILineGroup as lg -> TerminalEquivalent.LineGroup lg
                        | :? IBlockGroup as bg -> TerminalEquivalent.BlockGroup bg
                        | :? VirtualTerminal as vt -> TerminalEquivalent.VirtualTerminal vt
                        | _ -> BuilderCommon.throwCustomDesigntimeFarkle()
                    terminalEquivalents.Add(te.CreateNamed name)

        visit df
        while nonterminalsToProcess.Count <> 0 do
            ct.ThrowIfCancellationRequested()
            let nont = nonterminalsToProcess.Dequeue()
            for prod in nont.FreezeAndGetProductions() do
                productions.Add(nont, prod)
                for x in prod.Members do visit x

        if nonterminals.Count = 0 then
            Debug.Assert(terminalEquivalents.Count = 1 && productions.Count = 0)
            let (Named(name, _)) = terminalEquivalents.[0]
            let nont = PlaceholderNonterminal(df)
            nonterminals.Add(Named(name, nont :> _))
            productions.Add(nont :> _, nont.SingleProduction)

        {
            Metadata = metadata
            TerminalEquivalents = terminalEquivalents
            Nonterminals = nonterminals
            Productions = productions
            OperatorScopes = operatorScopes
        }
