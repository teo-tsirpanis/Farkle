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
            Choice1Of2 terminalMap.[term]
        | Choice1Of2 term ->
            let symbol = Terminal(uint32 terminals.Count, term.Name)
            terminalMap.Add(term, symbol)
            // For every addition to the terminals,
            // a corresponding one will be made to the transformers.
            // This way, the indices of the terminals and their transformers will match.
            terminals.Add(symbol)
            transformers.Add(term.Transformer)
            dfaSymbols <- (term.Regex, Choice1Of4 symbol) :: dfaSymbols
            Choice1Of2 symbol
        | Choice2Of2 nont when nonterminalMap.ContainsKey(nont) ->
            Choice2Of2 nonterminalMap.[nont]
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
            Choice2Of2 symbol
    let startSymbol =
        match df |> Symbol.specialize |> impl with
        // Our grammar is made of only one terminal.
        // We will create a production which will be made of just that.
        | Choice1Of2 term ->
            let root = Nonterminal(uint32 nonterminals.Count, term.Name)
            nonterminals.Add(root)
            productions.Add {
                Index = uint32 productions.Count
                Head = root
                Handle = ImmutableArray.Empty.Add(Choice1Of2 term)
            }
            fusers.Add(fun xs -> xs.[0])
            root
        | Choice2Of2 nont -> nont
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
