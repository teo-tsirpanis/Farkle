// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Builder.LALRConflictResolution
open Farkle.Common
open Farkle.Grammar
open Farkle.Monads.Either
open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Reflection

[<NoComparison; ReferenceEquality>]
/// An object containing the symbols of a grammar,
/// but lacking the LALR and DFA states.
type GrammarDefinition = {
    Metadata: GrammarMetadata
    StartSymbol: Nonterminal
    Symbols: Symbols
    Productions: ImmutableArray<Production>
    Groups: ImmutableArray<Group>

    DFASymbols: (Regex * DFASymbol) list

    ConflictResolver: LALRConflictResolver
}

type private PostProcessorDefinition = {
    Transformers: TransformerData []
    Fusers: FuserData []
}

[<RequireQualifiedAccess>]
/// A strongly-typed representation of a `DesigntimeFarkle`.
type private Symbol =
    | Terminal of AbstractTerminal
    | Nonterminal of AbstractNonterminal
    | LineGroup of AbstractLineGroup
    | BlockGroup of AbstractBlockGroup
    | VirtualTerminal of VirtualTerminal
    | Literal of string
    | NewLine

module private Symbol =
    let rec create (x: DesigntimeFarkle): Symbol =
        match x with
        | :? AbstractTerminal as term -> Symbol.Terminal term
        | :? AbstractNonterminal as nont -> Symbol.Nonterminal nont
        | :? AbstractLineGroup as lg -> Symbol.LineGroup lg
        | :? AbstractBlockGroup as bg -> Symbol.BlockGroup bg
        | :? VirtualTerminal as vt -> Symbol.VirtualTerminal vt
        | :? Literal as lit -> Symbol.Literal lit.Content
        | :? NewLine -> Symbol.NewLine
        | :? DesigntimeFarkleWrapper as x -> create x.InnerDesigntimeFarkle
        | _ -> invalidOp "Using a custom implementation of the \
DesigntimeFarkle interface is not allowed."

/// Functions to create `Grammar`s
/// and `PostProcessor`s from `DesigntimeFarkle`s.
module DesigntimeFarkleBuild =

    let private fdAsIs0 = FuserData.CreateAsIs 0

    // Memory conservation to the rescue! 👅
    let private noiseNewLine = Noise Terminal.NewLineName
    let private newLineRegex = Regex.chars "\r\n" <|> Regex.string "\r\n"
    let private commentSymbol = Noise "Comment"
    let private whitespaceSymbol = Noise "Whitespace"
    let private whitespaceRegex = Regex.chars BuilderCommon.whitespaceCharacters |> Regex.plus
    let private whitespaceRegexNoNewline = Regex.chars BuilderCommon.whitespaceCharactersNoNewLine |> Regex.plus

    let rec private addOperatorScope (set: HashSet<_>) (df: DesigntimeFarkle) =
        match df with
        | :? DesigntimeFarkleWithOperatorScope as dfog ->
            set.Add(dfog.OperatorScope) |> ignore
        | :? DesigntimeFarkleWrapper as dfw ->
            addOperatorScope set dfw.InnerDesigntimeFarkle
        | _ -> ()

    let private createGrammarDefinitionEx (df: DesigntimeFarkle) =
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
        let metadata = df.Metadata
        let terminals = ImmutableArray.CreateBuilder()
        let literalMap =
            match metadata.CaseSensitive with
            | true -> StringComparer.Ordinal
            | false -> StringComparer.OrdinalIgnoreCase
            |> Dictionary
        let terminalMap = Dictionary()
        let virtualTerminalMap = Dictionary()
        let transformers = ResizeArray()
        let nonterminals = ImmutableArray.CreateBuilder()
        let nonterminalMap = Dictionary()
        let noiseSymbols = ImmutableArray.CreateBuilder()
        let groups = ImmutableArray.CreateBuilder()
        let groupMap = Dictionary<AbstractGroup,_>()
        let productions = ImmutableArray.CreateBuilder()
        let fusers = ResizeArray()

        let operatorScopes = HashSet()
        let terminalObjects = Dictionary()
        let productionTokens = Dictionary()

        let nonterminalsToProcess = Queue()

        let getLALRSymbol (df: DesigntimeFarkle) =
            let newTerminal name =
                let namePatched =
                    if Terminal.IsNamedNewLine name then
                        // In Farkle's grammar domain model, groups can end by either a group end symbol,
                        // or a newline. Newlines are considered the terminals that are case-insensitively
                        // named "NewLine". However, another such terminal can exist and can cause unexplained
                        // errors when put inside a line group. This problem does not exist in GOLD Parser,
                        // nor did in Farkle's first grammar domain model, where the terminal that ended a
                        // group was explicitly specified. Prepending an underscore to the names of terminals
                        // that could be misrepresented as newlines is the least breaking fix, until the
                        // domain model gets overhauled in the next major version.
                        // TODO: Remove this temporary workaround.
                        "_" + name
                    else
                        name
                Terminal(uint32 terminals.Count, namePatched)

            let addTerminal term fTransformer =
                terminals.Add term
                transformers.Add fTransformer

            let addTerminalGroup name term transformer gStart gEnd =
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
                addTerminal term transformer

            let handleTerminal dfName (term: AbstractTerminal) =
                let symbol = newTerminal dfName
                terminalMap.Add(term, symbol)
                // For every addition to the terminals,
                // a corresponding one will be made to the transformers.
                // This way, the indices of the terminals and their transformers will match.
                addTerminal symbol term.Transformer
                addDFASymbol term.Regex (Choice1Of4 symbol)
                symbol

            addOperatorScope operatorScopes df
            let dfName = df.Name

            let builderSym = Symbol.create df
            let lalrSym =
                match builderSym with
                | Symbol.Terminal term when terminalMap.ContainsKey(term) ->
                    LALRSymbol.Terminal terminalMap.[term]
                | Symbol.Terminal term -> handleTerminal dfName term |> LALRSymbol.Terminal
                | Symbol.VirtualTerminal vt when virtualTerminalMap.ContainsKey(vt) ->
                    LALRSymbol.Terminal virtualTerminalMap.[vt]
                | Symbol.VirtualTerminal vt ->
                    let symbol = newTerminal dfName
                    virtualTerminalMap.Add(vt, symbol)
                    // The post-processor will never see a virtual terminal anyway.
                    addTerminal symbol TransformerData.Null
                    LALRSymbol.Terminal symbol
                | Symbol.Literal lit when literalMap.ContainsKey(lit) ->
                    LALRSymbol.Terminal literalMap.[lit]
                | Symbol.Literal lit ->
                    let term = Terminal.Create(lit, (Regex.string lit)) :?> AbstractTerminal
                    let symbol = handleTerminal dfName term
                    literalMap.Add(lit, symbol)
                    LALRSymbol.Terminal symbol
                | Symbol.NewLine ->
                    usesNewLine <- true
                    match newLineSymbol with
                    | Choice1Of4 nlTerminal -> nlTerminal
                    | _ ->
                        let nlTerminal = Terminal(uint32 terminals.Count, Terminal.NewLineName)
                        addTerminal nlTerminal TransformerData.Null
                        newLineSymbol <- Choice1Of4 nlTerminal
                        nlTerminal
                    |> LALRSymbol.Terminal
                | Symbol.Nonterminal nont when nonterminalMap.ContainsKey(nont) ->
                    LALRSymbol.Nonterminal nonterminalMap.[nont]
                | Symbol.Nonterminal nont ->
                    let symbol = Nonterminal(uint32 nonterminals.Count, dfName)
                    nonterminalMap.Add(nont, symbol)
                    nonterminals.Add(symbol)
                    nont.Freeze()
                    nonterminalsToProcess.Enqueue(symbol, nont)
                    LALRSymbol.Nonterminal symbol
                | Symbol.LineGroup lg when groupMap.ContainsKey lg ->
                    LALRSymbol.Terminal groupMap.[lg]
                | Symbol.LineGroup lg ->
                    // We don't know yet if the grammar is line-based, so
                    // we queue it until the entire grammar is traversed.
                    let term = newTerminal dfName
                    groupMap.[lg] <- term
                    addTerminalGroup dfName term lg.Transformer lg.GroupStart None
                    LALRSymbol.Terminal term
                | Symbol.BlockGroup bg when groupMap.ContainsKey bg ->
                    LALRSymbol.Terminal groupMap.[bg]
                | Symbol.BlockGroup bg ->
                    let term = newTerminal dfName
                    let gEnd = GroupEnd bg.GroupEnd
                    groupMap.[bg] <- term
                    addTerminalGroup dfName term bg.Transformer bg.GroupStart (Some gEnd)
                    LALRSymbol.Terminal term

            match lalrSym, builderSym with
            | LALRSymbol.Terminal term, Symbol.Literal lit ->
                terminalObjects.TryAdd(term, box lit) |> ignore
            | LALRSymbol.Terminal term, _ ->
                terminalObjects.TryAdd(term, box df) |> ignore
            | _ -> ()

            lalrSym

        let startSymbol =
            match getLALRSymbol df with
            // Our grammar is made of only one terminal.
            // We will create a production which will be made of just that.
            | LALRSymbol.Terminal (Terminal(_, name)) as t ->
                let root = Nonterminal(uint32 nonterminals.Count, name)
                nonterminals.Add(root)
                productions.Add {
                    Index = uint32 productions.Count
                    Head = root
                    Handle = ImmutableArray.Create(t)
                }
                fusers.Add(fdAsIs0)
                root
            | LALRSymbol.Nonterminal nont -> nont

        while nonterminalsToProcess.Count <> 0 do
            let grammarNont, abstractNont = nonterminalsToProcess.Dequeue()

            for abstractProd in abstractNont.Productions do
                let handle =
                    let b = ImmutableArray.CreateBuilder(abstractProd.Members.Length)
                    for x in abstractProd.Members do
                        b.Add(getLALRSymbol x)
                    b.MoveToImmutable()
                let production =
                    {Index = uint32 productions.Count; Head = grammarNont; Handle = handle}
                match abstractProd.ContextualPrecedenceToken with
                | null -> ()
                | cpToken -> productionTokens.Add(production, cpToken)
                productions.Add production
                fusers.Add abstractProd.Fuser

        let handleComment comment =
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

        // Add miscellaneous noise symbols.
        for name, regex in metadata.NoiseSymbols do
            let symbol = Noise name
            noiseSymbols.Add(symbol)
            addDFASymbol regex (Choice2Of4 symbol)
        // Add explicitly created comments.
        for comment in metadata.Comments do
            handleComment comment

        // Add the comment noise symbol once and only if it is needed.
        if not metadata.Comments.IsEmpty then
            noiseSymbols.Add(commentSymbol)

        // Add whitespace as noise, only if it is enabled.
        // If it is, we have to be careful not to consider
        // newlines as whitespace if we are on a line-based grammar.
        if metadata.AutoWhitespace then
            noiseSymbols.Add(whitespaceSymbol)
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
            | Choice2Of4 x -> noiseSymbols.Add(x)
            | _ -> ()
            addDFASymbol newLineRegex newLineSymbol

        let symbols = {
            Terminals = terminals.ToImmutable()
            Nonterminals = nonterminals.ToImmutable()
            NoiseSymbols = noiseSymbols.ToImmutable()
        }

        let resolver =
            PrecedenceBasedConflictResolver(operatorScopes, terminalObjects, productionTokens, metadata.CaseSensitive)
        {
            Metadata = df.Metadata
            StartSymbol = startSymbol
            Symbols = symbols
            Productions = productions.ToImmutable()
            Groups = groups.ToImmutable()
            DFASymbols = dfaSymbols
            ConflictResolver = resolver
        },
        {
            Transformers = transformers.ToArray()
            Fusers = fusers.ToArray()
        }

    /// Creates a `GrammarDefinition` from an untyped `DesigntimeFarkle`.
    [<CompiledName("CreateGrammarDefinition")>]
    let createGrammarDefinition df =
        createGrammarDefinitionEx df |> fst

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

    /// This value contains the name and version of that
    /// amazing piece of software that created this grammar.
    let private generatedWithLoveBy =
        let asm = Assembly.GetExecutingAssembly()
        sprintf "%s %s" (asm.GetName().Name) (Reflection.getAssemblyInformationalVersion asm)

    [<RequiresExplicitTypeArguments>]
    let private createPostProcessor<'TOutput> ppDef =
        PostProcessorCreator.create<'TOutput>
            ppDef.Transformers ppDef.Fusers

    let private failIfNotEmpty xs =
        match List.ofSeq xs with
        | [] -> Ok ()
        | xs -> Error xs

    let private buildDFAAndCheckForNullableSymbol caseSensitive symbols =
        DFABuild.buildRegexesToDFA true caseSensitive symbols
        |> Result.bind (fun x ->
            match x.[0].AcceptSymbol with
            | Some x -> Error [BuildError.NullableSymbol x]
            | None -> Ok x)

    /// Creates a `Grammar` from a `GrammarDefinition`.
    [<CompiledName("BuildGrammarOnly")>]
    let buildGrammarOnly grammarDef = either {
        do! aPrioriConsistencyCheck grammarDef |> failIfNotEmpty

        let! myDarlingLALRStateTable, mySweetDFAStateTable =
            Result.combine
                (LALRBuild.buildProductionsToLALRStates
                    grammarDef.ConflictResolver
                    grammarDef.StartSymbol
                    grammarDef.Symbols.Terminals
                    grammarDef.Symbols.Nonterminals
                    grammarDef.Productions)
                (buildDFAAndCheckForNullableSymbol
                    grammarDef.Metadata.CaseSensitive
                    grammarDef.DFASymbols)

        let properties = {
            Name = let (Nonterminal(_, name)) = grammarDef.StartSymbol in name
            CaseSensitive = grammarDef.Metadata.CaseSensitive
            AutoWhitespace = grammarDef.Metadata.AutoWhitespace
            GeneratedBy = generatedWithLoveBy
            GeneratedDate = DateTime.Now
            Source = GrammarSource.Built
        }

        return {
            _Properties = properties
            _StartSymbol = grammarDef.StartSymbol
            _Symbols = grammarDef.Symbols
            _Productions = grammarDef.Productions
            _Groups = grammarDef.Groups
            _LALRStates = myDarlingLALRStateTable
            _DFAStates = mySweetDFAStateTable
        }
    }

    /// Creates a `Grammar` and a `PostProcessor` from a typed `DesigntimeFarkle`.
    /// The construction of the grammar may fail. In this case, the output of the
    /// post-processor is indeterminate. Using this function (and all others in this
    /// module) will always build a new grammar, even if a precompiled one is available.
    [<CompiledName("Build")>]
    let build (df: DesigntimeFarkle<'TOutput>) =
        let myLovelyGrammarDefinition, myWonderfulPostProcessorDefinition = createGrammarDefinitionEx df
        let myFavoritePostProcessor = createPostProcessor<'TOutput> myWonderfulPostProcessorDefinition
        let myDearestGrammar = buildGrammarOnly myLovelyGrammarDefinition
        myDearestGrammar, myFavoritePostProcessor

    /// Creates a `PostProcessor` from the given `DesigntimeFarkle`.
    /// By not creating a grammar, some potentially expensive steps are skipped.
    /// This function is useful only for some very limited scenarios, such as
    /// having many designtime Farkles with an identical grammar but different post-processors.
    [<CompiledName("BuildPostProcessorOnly")>]
    let buildPostProcessorOnly (df: DesigntimeFarkle<'TOutput>) =
        df
        |> createGrammarDefinitionEx
        |> snd
        |> createPostProcessor<'TOutput>
