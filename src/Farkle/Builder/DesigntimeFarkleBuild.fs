// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.DesigntimeFarkleBuild

open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.PostProcessor
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection

/// A grammar type with some more information that is needed by the builder.
type private Grammar = {
    Metadata: GrammarMetadata
    Properties: ImmutableDictionary<string, string>
    StartSymbol: Nonterminal
    Symbols: Symbols
    Productions: ImmutableArray<Production>
    Groups: ImmutableArray<Group>

    DFASymbols: (Regex * DFASymbol) list

    Transformers: ImmutableArray<T<obj>>
    Fusers: ImmutableArray<obj[] -> obj>
}

/// Creates a `Grammar` object from a `DesigntimeFarkle`.
let private createDesigntimeGrammar (df: DesigntimeFarkle) =
    let mutable dfaSymbols = []
    let terminals = ImmutableArray.CreateBuilder()
    let terminalMap = Dictionary()
    let transformers = ImmutableArray.CreateBuilder()
    let nonterminals = ImmutableArray.CreateBuilder()
    let nonterminalMap = Dictionary()
    let productions = ImmutableArray.CreateBuilder()
    let fusers = ImmutableArray.CreateBuilder()
    let rec impl (sym: Symbol) =
        match sym with
        | Choice1Of2 term when terminalMap.ContainsKey(term) ->
            LALRSymbol.Terminal terminalMap.[term]
        | Choice1Of2 term ->
            let symbol = Terminal(uint32 terminals.Count, term.Name)
            terminalMap.Add(term, symbol)
            // For every addition to the terminals,
            // a corresponding one will be made to the transformers.
            // This way, the indices of the terminals and their transformers will match.
            terminals.Add(symbol)
            transformers.Add(term.Transformer)
            dfaSymbols <- (term.Regex, Choice1Of4 symbol) :: dfaSymbols
            LALRSymbol.Terminal symbol
        | Choice2Of2 nont when nonterminalMap.ContainsKey(nont) ->
            LALRSymbol.Nonterminal nonterminalMap.[nont]
        | Choice2Of2 nont ->
            let symbol = Nonterminal(uint32 nonterminals.Count, nont.Name)
            nonterminalMap.Add(nont, symbol)
            nonterminals.Add(symbol)
            nont.Productions
            |> List.iter (fun aprod ->
                let handle =
                    aprod.Members
                    |> Seq.map impl
                    |> ImmutableArray.CreateRange
                let prod = {Index = uint32 productions.Count; Head = symbol; Handle = handle}
                productions.Add(prod)
                fusers.Add(aprod.Fuse))
            LALRSymbol.Nonterminal symbol
    let startSymbol =
        match df |> Symbol.specialize |> impl with
        // Our grammar is made of only one terminal.
        // We will create a production which will be made of just that.
        | LALRSymbol.Terminal term ->
            let root = Nonterminal(uint32 nonterminals.Count, term.Name)
            nonterminals.Add(root)
            productions.Add {
                Index = uint32 productions.Count
                Head = root
                Handle = ImmutableArray.Empty.Add(LALRSymbol.Terminal term)
            }
            fusers.Add(fun xs -> xs.[0])
            root
        | LALRSymbol.Nonterminal nont -> nont
    let symbols = {
        Terminals = terminals.ToImmutable()
        Nonterminals = nonterminals.ToImmutable()
        NoiseSymbols = ImmutableArray.Empty
    }
    let properties =
        ImmutableDictionary.Empty
            .Add("Name", df.Name)
            .Add("Case Sensitive", string df.Metadata.CaseSensitive)
            .Add("Start Symbol", string startSymbol)
    {
        Metadata = df.Metadata
        Properties = properties
        StartSymbol = startSymbol
        Symbols = symbols
        Productions = productions.ToImmutable()
        Groups = ImmutableArray.Empty
        DFASymbols = dfaSymbols
        Transformers = transformers.ToImmutable()
        Fusers = fusers.ToImmutable()
    }

/// Performs some checks on the grammar that would cause problems later.
let private consistencyCheck grammar = either {
    let emptyNonterminals = HashSet grammar.Symbols.Nonterminals
    grammar.Productions |> Seq.iter (fun x -> emptyNonterminals.Remove(x.Head) |> ignore)
    if emptyNonterminals.Count <> 0 then
        do! emptyNonterminals |> set |> BuildErrorType.EmptyNonterminals |> Error

    let duplicateProductions =
        grammar.Productions
        |> Seq.countBy (fun x -> x.Head, x.Handle)
        |> Seq.filter (snd >> (<>) 1)
        |> Seq.map fst
        |> set
    if not duplicateProductions.IsEmpty then
        do! duplicateProductions |> BuildErrorType.DuplicateProductions |> Error
}

/// This value contains the name and version of the
/// amazing software that created this grammar.
let private generatedWithLoveBy =
    let asm = Assembly.GetExecutingAssembly()
    asm.GetCustomAttributes<AssemblyInformationalVersionAttribute>()
    |> Seq.tryExactlyOne
    |> Option.map (fun x -> x.InformationalVersion)
    |> Option.defaultWith (fun () -> asm.GetName().Version.ToString())
    |> sprintf "%s %s" (asm.GetName().Name)

[<RequiresExplicitTypeArguments>]
let private createPostProcessor<'TOutput> {Transformers = transformers; Fusers = fusers} =
    {
        new PostProcessor<'TOutput> with
                member __.Transform(term, pos, data) = transformers.[int term.Index].Invoke(pos, data)
                member __.Fuse(prod, members) = fusers.[int prod.Index] members
    }

let build (df: DesigntimeFarkle<'TOutput>) =
    let myLovelyBuilderGrammar = createDesigntimeGrammar df
    let myFavoritePostProcessor = createPostProcessor<'TOutput> myLovelyBuilderGrammar
    let myDearestGrammarGrammar = either {
        do! consistencyCheck myLovelyBuilderGrammar
        let! myDarlingLALRStateTable =
            LALRBuild.buildProductionsToLALRStates
                myLovelyBuilderGrammar.Symbols.Terminals.Length
                myLovelyBuilderGrammar.Symbols.Nonterminals.Length
                myLovelyBuilderGrammar.StartSymbol
                myLovelyBuilderGrammar.Productions
        let! mySweetDFAStateTable =
            DFABuild.buildRegexesToDFA
                myLovelyBuilderGrammar.Metadata.CaseSensitive
                myLovelyBuilderGrammar.DFASymbols
        return {
            _Properties =
                myLovelyBuilderGrammar.Properties
                    .Add("Generated Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                    .Add("Generated By", generatedWithLoveBy)
            _StartSymbol = myLovelyBuilderGrammar.StartSymbol
            _Symbols = myLovelyBuilderGrammar.Symbols
            _Productions = myLovelyBuilderGrammar.Productions
            _Groups = myLovelyBuilderGrammar.Groups
            _LALRStates = myDarlingLALRStateTable
            _DFAStates = mySweetDFAStateTable
        }
    }
    myDearestGrammarGrammar, myFavoritePostProcessor
