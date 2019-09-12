// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to create `Farkle.Grammar.Grammar`s
/// and `PostProcessor`s from `DesigntimeFarkle`s.
module Farkle.Builder.DesigntimeFarkleBuild

open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.PostProcessor
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection

[<NoComparison; ReferenceEquality>]
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

// Memory conservation to the rescue! 👅
let private noiseNewLine = Noise "NewLine"
let private newLineRegex = Regex.oneOf "\r\n" <|> Regex.literal "\r\n"
let private commentSymbol = Noise "Comment"
let private whitespaceSymbol = Noise "Whitespace"
let private whitespaceRegex = Regex.oneOf ['\t'; '\n'; '\r'; ' '] |> Regex.atLeast 1
let private whitespaceRegexNoNewline = Regex.oneOf ['\t'; ' '] |> Regex.atLeast 1

/// Creates a `Grammar` object from a `DesigntimeFarkle`.
let private createDesigntimeGrammar (df: DesigntimeFarkle) =
    let mutable dfaSymbols = []
    let mutable usesNewLine = false
    let metadata = df.Metadata
    let terminals = ImmutableArray.CreateBuilder()
    let literalMap =
        match metadata.CaseSensitive with
        | true -> StringComparer.Ordinal
        | false -> StringComparer.OrdinalIgnoreCase
        |> Dictionary
    let terminalMap = Dictionary()
    let transformers = ImmutableArray.CreateBuilder()
    let nonterminals = ImmutableArray.CreateBuilder()
    let nonterminalMap = Dictionary()
    let noiseSymbols = ImmutableArray.CreateBuilder()
    let groups = ImmutableArray.CreateBuilder()
    let productions = ImmutableArray.CreateBuilder()
    let fusers = ImmutableArray.CreateBuilder()
    let rec impl (sym: Symbol) =
        let handleTerminal (term: Farkle.Builder.Terminal) =
            let symbol = Terminal(uint32 terminals.Count, term.Name)
            terminalMap.Add(term, symbol)
            // For every addition to the terminals,
            // a corresponding one will be made to the transformers.
            // This way, the indices of the terminals and their transformers will match.
            terminals.Add(symbol)
            transformers.Add(term.Transformer)
            dfaSymbols <- (term.Regex, Choice1Of4 symbol) :: dfaSymbols
            symbol
        match sym with
        | Choice1Of3 term when terminalMap.ContainsKey(term) ->
            LALRSymbol.Terminal terminalMap.[term]
        | Choice1Of3 term -> handleTerminal term |> LALRSymbol.Terminal
        | Choice2Of3 (Literal lit) when literalMap.ContainsKey(lit) ->
            LALRSymbol.Terminal literalMap.[lit]
        | Choice2Of3 (Literal lit) ->
            let term = Terminal.Create lit tNull (Regex.literal lit) :?> Farkle.Builder.Terminal
            let symbol = handleTerminal term
            literalMap.Add(lit, symbol)
            LALRSymbol.Terminal symbol
        | Choice3Of3 nont when nonterminalMap.ContainsKey(nont) ->
            LALRSymbol.Nonterminal nonterminalMap.[nont]
        | Choice3Of3 nont ->
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
    metadata.NoiseSymbols
    |> Seq.iter (fun (name, regex) ->
        let symbol = Noise name
        noiseSymbols.Add(symbol)
        dfaSymbols <- (regex, Choice2Of4 symbol) :: dfaSymbols)
    metadata.Comments
    |> Seq.iter (function
        | LineComment cStart ->
            let startSymbol = GroupStart(cStart, uint32 groups.Count)
            dfaSymbols <- (Regex.literal cStart, Choice3Of4 startSymbol) :: dfaSymbols
            usesNewLine <- true
            groups.Add {
                Name = "Comment Line"
                ContainerSymbol = Choice2Of2 commentSymbol
                Start = startSymbol
                End = Choice2Of3 noiseNewLine
                AdvanceMode = AdvanceMode.Character
                EndingMode = EndingMode.Open
                Nesting = ImmutableHashSet.Empty
            }
        | BlockComment(cStart, cEnd) ->
            let startSymbol = GroupStart(cStart, uint32 groups.Count)
            let endSymbol = GroupEnd cEnd
            dfaSymbols <-
                (Regex.literal cStart, Choice3Of4 startSymbol) ::
                (Regex.literal cEnd, Choice4Of4 endSymbol) :: dfaSymbols
            groups.Add {
                Name = "Comment Block"
                ContainerSymbol = Choice2Of2 commentSymbol
                Start = startSymbol
                End = Choice3Of3 endSymbol
                AdvanceMode = AdvanceMode.Character
                EndingMode = EndingMode.Closed
                Nesting = ImmutableHashSet.Empty
            }
    )
    if not metadata.Comments.IsEmpty then
        noiseSymbols.Add(commentSymbol)
    if metadata.AutoWhitespace then
        noiseSymbols.Add(whitespaceSymbol)
        let whitespaceRegex =
            if usesNewLine then
                whitespaceRegexNoNewline
            else
                whitespaceRegex
        dfaSymbols <- (whitespaceRegex, Choice2Of4 whitespaceSymbol) :: dfaSymbols
    if usesNewLine then
        noiseSymbols.Add(noiseNewLine)
        dfaSymbols <- (newLineRegex, Choice2Of4 noiseNewLine) :: dfaSymbols
    let symbols = {
        Terminals = terminals.ToImmutable()
        Nonterminals = nonterminals.ToImmutable()
        NoiseSymbols = noiseSymbols.ToImmutable()
    }
    let properties =
        ImmutableDictionary.Empty
            .Add("Name", df.Name)
            .Add("Case Sensitive", string metadata.CaseSensitive)
            .Add("Start Symbol", string startSymbol)
            .Add("Auto Whitespace", string metadata.AutoWhitespace)
    {
        Metadata = df.Metadata
        Properties = properties
        StartSymbol = startSymbol
        Symbols = symbols
        Productions = productions.ToImmutable()
        Groups = groups.ToImmutable()
        DFASymbols = dfaSymbols
        Transformers = transformers.ToImmutable()
        Fusers = fusers.ToImmutable()
    }

/// Performs some checks on the grammar that would cause problems later.
let private consistencyCheck grammar = either {
    let emptyNonterminals = HashSet grammar.Symbols.Nonterminals
    grammar.Productions |> Seq.iter (fun x -> emptyNonterminals.Remove(x.Head) |> ignore)
    if emptyNonterminals.Count <> 0 then
        do! emptyNonterminals |> Seq.map (fun x -> x.Name) |> set |> BuildError.EmptyNonterminals |> Error

    let duplicateProductions =
        grammar.Productions
        |> Seq.countBy (fun x -> x.Head, x.Handle)
        |> Seq.filter (snd >> (<>) 1)
        |> Seq.map fst
        |> set
    if not duplicateProductions.IsEmpty then
        do! duplicateProductions |> BuildError.DuplicateProductions |> Error
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

/// Creates a `Farkle.Grammar.Grammar` and a `PostProcessor`
/// from the given `DesigntimeFarkle`.
/// The construction of the grammar may fail.
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
