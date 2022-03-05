// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Builder.LALRConflictResolution
open Farkle.Common
open Farkle.Grammars
open Farkle.Monads.Either
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection
open System.Threading

/// An object containing the symbols of a grammar,
/// but lacking the LALR and DFA states.
[<NoComparison; ReferenceEquality>]
type GrammarDefinition = {
    Properties: GrammarProperties
    StartSymbol: Nonterminal
    Symbols: Symbols
    Productions: ImmutableArray<Production>
    Groups: ImmutableArray<Group>

    DFASymbols: (Regex * DFASymbol) list

    ConflictResolver: LALRConflictResolver
}

// Compares objects for equality. Designtime Farkles are considered
// equal if they reference the same symbol regardless of any metadata
// changes. A designtime Farkle literal is considered equal to a string
// if the literal's string is equal.
[<Sealed>]
type private OperatorKeyComparer private(innerComparer: IEqualityComparer<obj>) =
    inherit EqualityComparer<obj>()
    static let getKey (x: obj) =
        match x with
        | :? DesigntimeFarkle as df -> DesigntimeFarkle.getIdentityObject df
        | _ -> x
    static let caseSensitive = FallbackStringComparers.caseSensitive |> OperatorKeyComparer
    static let caseInsensitive = FallbackStringComparers.caseInsensitive |> OperatorKeyComparer
    static member Get isCaseSensitive =
        if isCaseSensitive then
            caseSensitive
        else
            caseInsensitive
    override _.Equals(x1, x2) = innerComparer.Equals(getKey x1, getKey x2)
    override _.GetHashCode x = x |> getKey |> innerComparer.GetHashCode

/// Functions to create `Grammar`s
/// and `PostProcessor`s from `DesigntimeFarkle`s.
module DesigntimeFarkleBuild =

    // Memory conservation to the rescue! 👅
    let private noiseNewLine = Noise Terminal.NewLineName
    let private newLineRegex = Regex.chars "\r\n" <|> Regex.string "\r\n"
    let private commentSymbol = Noise "Comment"
    let private whitespaceSymbol = Noise "Whitespace"
    let private whitespaceRegex = Regex.chars BuilderCommon.whitespaceCharacters |> Regex.plus
    let private whitespaceRegexNoNewline = Regex.chars BuilderCommon.whitespaceCharactersNoNewLine |> Regex.plus

    /// This value contains the name and version of that
    /// amazing piece of software that created this grammar.
    // TODO: Consider deleting it; it's unnecessary.
    let private generatedWithLoveBy =
        let asm = Assembly.GetExecutingAssembly()
        sprintf "%s %s" (asm.GetName().Name) (Reflection.getAssemblyInformationalVersion asm)

    let internal createGrammarDefinitionEx (dfDef: DesigntimeFarkleDefinition) =
        let mutable dfaSymbols = []
        let addDFASymbol regex sym =
            dfaSymbols <- (regex, sym) :: dfaSymbols
        // This variable holds whether there is a line comment
        // or a newline special terminal in the language.
        let mutable usesNewLine = false
        // If the above variable is set to true, this
        // DFASymbol will determine the representation
        // of the newline symbol. By default it is set to
        // a noise symbol. It can be also set to a terminal,
        // if they are needed by a production.
        let mutable newLineSymbol = Choice2Of4 noiseNewLine
        let metadata = dfDef.Metadata
        let lalrSymbolMap =
            Dictionary(dfDef.TerminalEquivalents.Count + dfDef.Nonterminals.Count,
                FallbackStringComparers.get metadata.CaseSensitive)
        let groups = ImmutableArray.CreateBuilder()

        let operatorKeys =
            // We won't create a smart conflict resolver if there are not any operator scopes.
            match dfDef.OperatorScopes.Count with
            | 0 -> None
            | _ -> Some(Dictionary(), Dictionary())

        let terminals =
            let b = ImmutableArray.CreateBuilder(dfDef.TerminalEquivalents.Count)
            let newTerminal name = Terminal(uint32 b.Count, name)

            let addTerminal term (identity: obj) =
                b.Add term
                lalrSymbolMap.Add(identity, LALRSymbol.Terminal term)

            let handleTerminal name (identity: obj) regex =
                let symbol = newTerminal name
                addTerminal symbol identity
                addDFASymbol regex (Choice1Of4 symbol)
                symbol

            let addTerminalGroup name (identity: obj) gStart gEnd =
                let term = newTerminal name
                let container = Choice1Of2 term
                let gStart' = GroupStart(gStart, uint32 groups.Count)
                addDFASymbol (Regex.string gStart) (Choice3Of4 gStart')
                match gEnd with
                | Some (GroupEnd str as ge) ->
                    addDFASymbol (Regex.string str) (Choice4Of4 ge)
                | None -> usesNewLine <- true
                groups.Add {
                    Name = name
                    ContainerSymbol = container
                    Start = gStart'
                    End = gEnd
                    AdvanceMode = AdvanceMode.Character
                    // We want to keep the NewLine for the rest of the tokenizer to see.
                    EndingMode =
                        match gEnd with
                        | Some _ -> EndingMode.Closed
                        | None -> EndingMode.Open
                    Nesting = ImmutableHashSet.Empty
                }
                addTerminal term identity
                term

            // We don't have to worry about diplicates, they are handled by DesigntimeFarkleAnalyze.
            for Named(name, te) in dfDef.TerminalEquivalents do
                let identity = te.IdentityObject
                
                let symbol =
                    match te with
                    | TerminalEquivalent.Terminal term -> handleTerminal name identity term.Regex
                    | TerminalEquivalent.Literal lit -> handleTerminal name identity (Regex.string lit)
                    | TerminalEquivalent.NewLine ->
                        usesNewLine <- true
                        let symbol = Terminal(uint32 b.Count, Terminal.NewLineName)
                        addTerminal symbol NewLine
                        newLineSymbol <- Choice1Of4 symbol
                        symbol
                    | TerminalEquivalent.LineGroup lg ->
                        addTerminalGroup name identity lg.GroupStart None
                    | TerminalEquivalent.BlockGroup bg ->
                        let gEnd = GroupEnd bg.GroupEnd
                        addTerminalGroup name identity bg.GroupStart (Some gEnd)
                    | TerminalEquivalent.VirtualTerminal _ ->
                        let symbol = newTerminal name
                        addTerminal symbol identity
                        symbol

                match operatorKeys with
                | Some (terminalKeys, _) ->
                    terminalKeys.TryAdd(symbol, te.IdentityObject) |> ignore
                | None -> ()
            b.MoveToImmutable()

        let struct(nonterminals, nonterminalMap) =
            let b = ImmutableArray.CreateBuilder(dfDef.Nonterminals.Count)
            let nonterminalMap = Dictionary(dfDef.Nonterminals.Count)
            for Named(name, nont) in dfDef.Nonterminals do
                let symbol = Nonterminal(uint32 b.Count, name)
                b.Add(symbol)
                lalrSymbolMap.Add(nont, LALRSymbol.Nonterminal symbol)
                nonterminalMap.Add(nont, symbol)
            b.MoveToImmutable(), nonterminalMap

        let productions =
            let b = ImmutableArray.CreateBuilder(dfDef.Productions.Count)
            for head, abstractProd in dfDef.Productions do
                let handle =
                    let b = ImmutableArray.CreateBuilder(abstractProd.Members.Length)
                    for x in abstractProd.Members do
                        b.Add lalrSymbolMap.[DesigntimeFarkle.getIdentityObject x]
                    b.MoveToImmutable()
                let production =
                    {Index = uint32 b.Count; Head = nonterminalMap.[head]; Handle = handle}
                match abstractProd.ContextualPrecedenceToken, operatorKeys with
                | null, _ | _, None -> ()
                | cpToken, Some(_, productionKeys) -> productionKeys.Add(production, cpToken)
                b.Add production
            b.MoveToImmutable()

        let startSymbol = nonterminals.[0]

        // Add explicitly created comments.
        for comment in metadata.Comments do
            let newStartSymbol name = GroupStart(name, uint32 groups.Count)
            let addGroup name gStart gEnd em =
                groups.Add {
                    Name = name
                    ContainerSymbol = Choice2Of2 commentSymbol
                    Start = gStart
                    End = gEnd
                    AdvanceMode = AdvanceMode.Character
                    EndingMode = em
                    Nesting = ImmutableHashSet.Empty
                }
            match comment with
            | LineComment cStart ->
                let startSymbol = newStartSymbol cStart
                addDFASymbol (Regex.string cStart) (Choice3Of4 startSymbol)
                usesNewLine <- true
                addGroup "Comment Line" startSymbol None EndingMode.Open
            | BlockComment(cStart, cEnd) ->
                let startSymbol = newStartSymbol cStart
                let endSymbol = GroupEnd cEnd
                addDFASymbol (Regex.string cStart) (Choice3Of4 startSymbol)
                addDFASymbol (Regex.string cEnd) (Choice4Of4 endSymbol)
                addGroup "Comment Block" startSymbol (Some endSymbol) EndingMode.Closed

        let noiseSymbols =
            let b = ImmutableArray.CreateBuilder()
            // Add miscellaneous noise symbols.
            for name, regex in metadata.NoiseSymbols do
                let symbol = Noise name
                b.Add(symbol)
                addDFASymbol regex (Choice2Of4 symbol)

            // Add the comment noise symbol once and only if it is needed.
            if not metadata.Comments.IsEmpty then
                b.Add(commentSymbol)

            // Add whitespace as noise, only if it is enabled.
            // If it is, we have to be careful not to consider
            // newlines as whitespace if we are on a line-based grammar.
            if metadata.AutoWhitespace then
                b.Add(whitespaceSymbol)
                let whitespaceRegex =
                    if usesNewLine then
                        whitespaceRegexNoNewline
                    else
                        whitespaceRegex
                addDFASymbol whitespaceRegex (Choice2Of4 whitespaceSymbol)

            // And finally, add the newline symbol to the DFA and to the noise symbols.
            // If it was a terminal, it is already included in the terminals.
            if usesNewLine then
                match newLineSymbol with
                | Choice2Of4 x -> b.Add(x)
                | _ -> ()
                addDFASymbol newLineRegex newLineSymbol
            b.ToImmutable()

        let symbols = {
            Terminals = terminals
            Nonterminals = nonterminals
            NoiseSymbols = noiseSymbols
        }

        let conflictResolver =
            match operatorKeys with
            | None -> LALRConflictResolver.Default
            | Some (terminalKeys, productionKeys) ->
                let resolverComparer = OperatorKeyComparer.Get metadata.CaseSensitive
                PrecedenceBasedConflictResolver(dfDef.OperatorScopes, terminalKeys, productionKeys, resolverComparer)

        let properties = {
            Name = let (Nonterminal(_, name)) = startSymbol in name
            CaseSensitive = metadata.CaseSensitive
            AutoWhitespace = metadata.AutoWhitespace
            GeneratedBy = generatedWithLoveBy
            GeneratedDate = DateTime.Now
            Source = GrammarSource.Built
        }

        {
            Properties = properties
            StartSymbol = startSymbol
            Symbols = symbols
            Productions = productions
            Groups = groups.ToImmutable()
            DFASymbols = dfaSymbols
            ConflictResolver = conflictResolver
        }

    /// Creates a `GrammarDefinition` from an untyped `DesigntimeFarkle`.
    [<CompiledName("CreateGrammarDefinition")>]
    let createGrammarDefinition df =
        DesigntimeFarkleAnalyze.analyze CancellationToken.None df
        |> createGrammarDefinitionEx

    /// Performs some checks on the grammar that would cause problems later.
    let private aPrioriConsistencyCheck grammar = seq {

        let emptyNonterminals = HashSet grammar.Symbols.Nonterminals
        for prod in grammar.Productions do
            emptyNonterminals.Remove prod.Head |> ignore
        for Nonterminal(_, name) in emptyNonterminals do
            yield BuildError.EmptyNonterminal name

        let duplicateProductions =
            grammar.Productions
            |> Seq.countBy (fun x -> x.Head, x.Handle)
            |> Seq.filter (snd >> (<>) 1)
            |> Seq.map fst
        for prod in duplicateProductions do
            yield BuildError.DuplicateProduction prod

        if grammar.Symbols.Terminals.Length > BuildError.SymbolLimit
            || grammar.Symbols.Nonterminals.Length > BuildError.SymbolLimit then
            yield BuildError.SymbolLimitExceeded
    }

    let private failIfNotEmpty xs =
        match List.ofSeq xs with
        | [] -> Ok ()
        | xs -> Error xs

    let private checkForNullableSymbol result =
        Result.bind (fun (x: ImmutableArray<DFAState>) ->
            match x.[0].AcceptSymbol with
            | Some x -> Error [BuildError.NullableSymbol x]
            | None -> Ok x) result

    /// Creates a `Grammar` from a `GrammarDefinition`. The operation
    /// can be cancelled, throwing an `OperationCanceledException`.
    /// It also accepts a `BuildOptions` object, allowing further configuration.
    [<CompiledName("BuildGrammarOnlyEx")>]
    let buildGrammarOnlyEx ct options grammarDef = either {
        do! aPrioriConsistencyCheck grammarDef |> failIfNotEmpty

        let! myDarlingLALRStateTable, mySweetDFAStateTable =
            Result.combine
                (LALRBuild.buildProductionsToLALRStatesEx
                    ct
                    options
                    grammarDef.ConflictResolver
                    grammarDef.StartSymbol
                    grammarDef.Symbols.Terminals
                    grammarDef.Symbols.Nonterminals
                    grammarDef.Productions)
                (DFABuild.buildRegexesToDFAEx
                    ct
                    options
                    true
                    grammarDef.Properties.CaseSensitive
                    grammarDef.DFASymbols
                |> checkForNullableSymbol)

        return {
            _Properties = grammarDef.Properties
            _StartSymbol = grammarDef.StartSymbol
            _Symbols = grammarDef.Symbols
            _Productions = grammarDef.Productions
            _Groups = grammarDef.Groups
            _LALRStates = myDarlingLALRStateTable
            _DFAStates = mySweetDFAStateTable
        }
    }

    /// Creates a `Grammar` from a `GrammarDefinition`.
    [<CompiledName("BuildGrammarOnly")>]
    let buildGrammarOnly grammarDef =
        buildGrammarOnlyEx CancellationToken.None BuildOptions.Default grammarDef

    /// Creates a `Grammar` and a `PostProcessor` from a typed `DesigntimeFarkle`.
    /// The construction of the grammar may fail. In this case, the output of the
    /// post-processor is indeterminate. Using this function (and all others in this
    /// module) will always build a new grammar, even if a precompiled one is available.
    /// This function also allows the build to be cancelled and further configured.
    [<CompiledName("BuildEx")>]
    let buildEx ct options (df: DesigntimeFarkle<'TOutput>) =
        let myWonderfulDesigntimeFarkleDefinition = DesigntimeFarkleAnalyze.analyze ct df
        let myLovelyGrammarDefinition = createGrammarDefinitionEx myWonderfulDesigntimeFarkleDefinition
        let myFavoritePostProcessor = PostProcessorCreator.create<'TOutput> myWonderfulDesigntimeFarkleDefinition
        let myDearestGrammar = buildGrammarOnlyEx ct options myLovelyGrammarDefinition
        myDearestGrammar, myFavoritePostProcessor

    /// Creates a `Grammar` and a `PostProcessor` from a typed `DesigntimeFarkle`.
    /// The construction of the grammar may fail. In this case, the output of the
    /// post-processor is indeterminate. Using this function (and all others in this
    /// module) will always build a new grammar, even if a precompiled one is available.
    [<CompiledName("Build")>]
    let build (df: DesigntimeFarkle<'TOutput>) =
        buildEx CancellationToken.None BuildOptions.Default df

    /// Creates a `PostProcessor` from the given `DesigntimeFarkle`.
    /// By not creating a grammar, some potentially expensive steps are skipped.
    /// This function is useful only for some very limited scenarios, such as
    /// having many designtime Farkles with an identical grammar but different post-processors.
    [<CompiledName("BuildPostProcessorOnly")>]
    let buildPostProcessorOnly (df: DesigntimeFarkle<'TOutput>) =
        df
        |> DesigntimeFarkleAnalyze.analyze CancellationToken.None
        |> PostProcessorCreator.create<'TOutput>
