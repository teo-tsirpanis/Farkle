// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.PostProcessor
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection

[<NoComparison; ReferenceEquality>]
/// A grammar type with some more information that is needed by the builder.
type GrammarDefinition = {
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

/// Functions to create `Grammar`s
/// and `PostProcessor`s from `DesigntimeFarkle`s.
module DesigntimeFarkleBuild =

    /// A delegate of type `T<obj>` that always returns null.
    let private tNull: T<obj> = T(fun _ _ -> null)

    // Memory conservation to the rescue! 👅
    let private noiseNewLine = Noise "NewLine"
    let private newLineRegex = Regex.oneOf "\r\n" <|> Regex.literal "\r\n"
    let private commentSymbol = Noise "Comment"
    let private whitespaceSymbol = Noise "Whitespace"
    let private whitespaceRegex = Regex.oneOf ['\t'; '\n'; '\r'; ' '] |> Regex.atLeast 1
    let private whitespaceRegexNoNewline = Regex.oneOf ['\t'; ' '] |> Regex.atLeast 1

    /// Creates a `GrammarDefinition` from an untyped `DesigntimeFarkle`.
    let createGrammarDefinition (df: DesigntimeFarkle) =
        let mutable dfaSymbols = []
        // This variable holds whether there is a line comment
        // or a newline special terminal in the language.
        let mutable usesNewLine = false
        // If the above variable is set to true, this
        // DFASymbol will determine the representation
        // of the newline symbol. By default it is set to
        // a noise symbol. It can be also set to a terminal,
        // if they are needed by a profuction.
        let mutable newLineSymbol = Choice2Of4 noiseNewLine
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
            let handleTerminal (term: AbstractTerminal) =
                let symbol = Terminal(uint32 terminals.Count, term.Name)
                terminalMap.Add(term, symbol)
                // For every addition to the terminals,
                // a corresponding one will be made to the transformers.
                // This way, the indices of the terminals and their transformers will match.
                terminals.Add(symbol)
                transformers.Add(if isNull term.Transformer then tNull else term.Transformer)
                dfaSymbols <- (term.Regex, Choice1Of4 symbol) :: dfaSymbols
                symbol
            match sym with
            | Choice1Of4 term when terminalMap.ContainsKey(term) ->
                LALRSymbol.Terminal terminalMap.[term]
            | Choice1Of4 term -> handleTerminal term |> LALRSymbol.Terminal
            | Choice2Of4 (Literal lit) when literalMap.ContainsKey(lit) ->
                LALRSymbol.Terminal literalMap.[lit]
            | Choice2Of4 (Literal lit) ->
                let term = Terminal.Create(lit, (Regex.literal lit)) :?> AbstractTerminal
                let symbol = handleTerminal term
                literalMap.Add(lit, symbol)
                LALRSymbol.Terminal symbol
            | Choice3Of4 NewLine ->
                usesNewLine <- true
                match newLineSymbol with
                    | Choice1Of4 nlTerminal -> LALRSymbol.Terminal nlTerminal
                    | _ ->
                        let nlTerminal = Terminal(uint32 terminals.Count, "NewLine")
                        nlTerminal
                        |> Choice1Of4
                        |> (fun nlTerminal ->
                            newLineSymbol <- nlTerminal
                            dfaSymbols <- (newLineRegex, nlTerminal) :: dfaSymbols)
                        LALRSymbol.Terminal nlTerminal
            | Choice4Of4 nont when nonterminalMap.ContainsKey(nont) ->
                LALRSymbol.Nonterminal nonterminalMap.[nont]
            | Choice4Of4 nont ->
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
                    Handle = ImmutableArray.Create(LALRSymbol.Terminal term)
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
                let endSymbol =
                    match newLineSymbol with
                    | Choice1Of4 term -> Choice1Of3 term
                    | Choice2Of4 noise -> Choice2Of3 noise
                    // We will never reach that line.
                    | _ -> failwith "Newline cannot be represented by something other than a terminal or a noise symbol."
                dfaSymbols <- (Regex.literal cStart, Choice3Of4 startSymbol) :: dfaSymbols
                usesNewLine <- true
                groups.Add {
                    Name = "Comment Line"
                    ContainerSymbol = Choice2Of2 commentSymbol
                    Start = startSymbol
                    End = endSymbol
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
        let nullableDFASymbols =
            grammar.DFASymbols
            |> List.filter (fun (regex, _) -> regex.IsNullable())
            |> List.map snd
            |> set
        if not nullableDFASymbols.IsEmpty then
            do! Error <| BuildError.NullableSymbols nullableDFASymbols

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

    /// This value contains the name and version of that
    /// amazing piece of software that created this grammar.
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

    /// Creates a `Grammar` from a `GrammarDefinition`.
    let buildGrammarOnly dg = either {
            do! consistencyCheck dg
            let! myDarlingLALRStateTable =
                LALRBuild.buildProductionsToLALRStates
                    dg.Symbols.Terminals.Length
                    dg.Symbols.Nonterminals.Length
                    dg.StartSymbol
                    dg.Productions
            let! mySweetDFAStateTable =
                DFABuild.buildRegexesToDFA
                    dg.Metadata.CaseSensitive
                    dg.DFASymbols
            return {
                _Properties =
                    dg.Properties
                        .Add("Generated Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                        .Add("Generated By", generatedWithLoveBy)
                _StartSymbol = dg.StartSymbol
                _Symbols = dg.Symbols
                _Productions = dg.Productions
                _Groups = dg.Groups
                _LALRStates = myDarlingLALRStateTable
                _DFAStates = mySweetDFAStateTable
            }
        }

    /// Creates a `Grammar` and a `PostProcessor`
    /// from a typed `DesigntimeFarkle`.
    /// The construction of the grammar may fail.
    let build (df: DesigntimeFarkle<'TOutput>) =
        let myLovelyBuilderGrammar = createGrammarDefinition df
        let myFavoritePostProcessor = createPostProcessor<'TOutput> myLovelyBuilderGrammar
        let myDearestGrammarGrammar = buildGrammarOnly myLovelyBuilderGrammar
        myDearestGrammarGrammar, myFavoritePostProcessor

    /// Creates a `PostProcessor` from the given `DesigntimeFarkle`.
    /// By not creating a grammar, some potentially expensive steps are skipped.
    /// This function is useful only for some very limited scenarios, such as
    /// having many designtime Farkles with an identical grammar but different post-processors.
    let buildPostProcessorOnly (df: DesigntimeFarkle<'TOutput>) =
        df |> createGrammarDefinition |> createPostProcessor<'TOutput>
