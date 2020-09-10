// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Grammar
open Farkle.Monads.Either
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
    Fusers: ImmutableArray<F<obj>>
}

[<RequireQualifiedAccess>]
/// A strongly-typed representation of a `DesigntimeFarkle`.
type internal Symbol =
    | Terminal of AbstractTerminal
    | Nonterminal of AbstractNonterminal
    | LineGroup of AbstractLineGroup
    | BlockGroup of AbstractBlockGroup
    | Literal of string
    | NewLine

module internal Symbol =
    let rec specialize (x: DesigntimeFarkle): Symbol =
        match x with
        | :? AbstractTerminal as term -> Symbol.Terminal term
        | :? AbstractNonterminal as nont -> Symbol.Nonterminal nont
        | :? AbstractLineGroup as lg -> Symbol.LineGroup lg
        | :? AbstractBlockGroup as bg -> Symbol.BlockGroup bg
        | :? Literal as lit -> let (Literal lit) = lit in Symbol.Literal lit
        | :? NewLine -> Symbol.NewLine
        | :? DesigntimeFarkleWrapper as x -> specialize x.InnerDesigntimeFarkle
        | _ -> invalidArg "x" "Using a custom implementation of the \
DesigntimeFarkle interface is not allowed."

/// Functions to create `Grammar`s
/// and `PostProcessor`s from `DesigntimeFarkle`s.
module DesigntimeFarkleBuild =

    let private tNull: T<obj> = T(fun _ _ -> null)
    let private fNull: F<obj> = F(fun _ -> null)

    // Memory conservation to the rescue! 👅
    let private noiseNewLine = Noise "NewLine"
    let private newLineRegex = Regex.chars "\r\n" <|> Regex.string "\r\n"
    let private commentSymbol = Noise "Comment"
    let private whitespaceSymbol = Noise "Whitespace"
    let private whitespaceRegex = Regex.chars ['\t'; '\n'; '\r'; ' '] |> Regex.atLeast 1
    let private whitespaceRegexNoNewline = Regex.chars ['\t'; ' '] |> Regex.atLeast 1

    /// Creates a `GrammarDefinition` from an untyped `DesigntimeFarkle`.
    [<CompiledName("CreateGrammarDefinition")>]
    let createGrammarDefinition (df: DesigntimeFarkle) =
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
        let newTerminal name = Terminal(uint32 terminals.Count, name)
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
        let groupMap = Dictionary<AbstractGroup,_>()
        let productions = ImmutableArray.CreateBuilder()
        let fusers = ImmutableArray.CreateBuilder()

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
            terminals.Add term
            transformers.Add transformer

        let rec getLALRSymbol (df: DesigntimeFarkle) =
            let handleTerminal dfName (term: AbstractTerminal) =
                let symbol = newTerminal dfName
                terminalMap.Add(term, symbol)
                // For every addition to the terminals,
                // a corresponding one will be made to the transformers.
                // This way, the indices of the terminals and their transformers will match.
                terminals.Add(symbol)
                transformers.Add(if isNull term.Transformer then tNull else term.Transformer)
                addDFASymbol term.Regex (Choice1Of4 symbol)
                symbol

            let dfName = df.Name
            match Symbol.specialize df with
            | Symbol.Terminal term when terminalMap.ContainsKey(term) ->
                LALRSymbol.Terminal terminalMap.[term]
            | Symbol.Terminal term -> handleTerminal dfName term |> LALRSymbol.Terminal
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
                    let nlTerminal = newTerminal "NewLine"
                    terminals.Add(nlTerminal)
                    transformers.Add(tNull)
                    newLineSymbol <- Choice1Of4 nlTerminal
                    nlTerminal
                |> LALRSymbol.Terminal
            | Symbol.Nonterminal nont when nonterminalMap.ContainsKey(nont) ->
                LALRSymbol.Nonterminal nonterminalMap.[nont]
            | Symbol.Nonterminal nont ->
                let symbol = Nonterminal(uint32 nonterminals.Count, nont.Name)
                nonterminalMap.Add(nont, symbol)
                nonterminals.Add(symbol)
                nont.Freeze()
                nont.Productions
                |> List.iter (fun aprod ->
                    let handle =
                        aprod.Members
                        |> Seq.map getLALRSymbol
                        |> ImmutableArray.CreateRange
                    let prod = {Index = uint32 productions.Count; Head = symbol; Handle = handle}
                    productions.Add(prod)
                    fusers.Add(if isNull aprod.Fuser then fNull else aprod.Fuser))
                LALRSymbol.Nonterminal symbol
            | Symbol.LineGroup lg when groupMap.ContainsKey lg ->
                LALRSymbol.Terminal groupMap.[lg]
            | Symbol.LineGroup lg ->
                // We don't know yet if the grammar is line-based, so
                // we queue it until the entire grammar is traversed.
                let term = newTerminal lg.Name
                groupMap.[lg] <- term
                addTerminalGroup lg.Name term lg.Transformer lg.GroupStart None
                LALRSymbol.Terminal term
            | Symbol.BlockGroup bg when groupMap.ContainsKey bg ->
                LALRSymbol.Terminal groupMap.[bg]
            | Symbol.BlockGroup bg ->
                let term = newTerminal bg.Name
                let gEnd = GroupEnd bg.GroupEnd
                groupMap.[bg] <- term
                addTerminalGroup bg.Name term bg.Transformer bg.GroupStart (Some gEnd)
                LALRSymbol.Terminal term

        let startSymbol =
            match getLALRSymbol df with
            // Our grammar is made of only one terminal.
            // We will create a production which will be made of just that.
            | LALRSymbol.Terminal term as t ->
                let root = Nonterminal(uint32 nonterminals.Count, term.Name)
                nonterminals.Add(root)
                productions.Add {
                    Index = uint32 productions.Count
                    Head = root
                    Handle = ImmutableArray.Create(t)
                }
                fusers.Add(fun xs -> xs.[0])
                root
            | LALRSymbol.Nonterminal nont -> nont

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
        Seq.iter (fun (name, regex) ->
            let symbol = Noise name
            noiseSymbols.Add(symbol)
            addDFASymbol regex (Choice2Of4 symbol)
        ) metadata.NoiseSymbols
        // Add explicitly created comments.
        Seq.iter handleComment metadata.Comments

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
    let private aPrioriConsistencyCheck grammar = either {

        let emptyNonterminals = HashSet grammar.Symbols.Nonterminals
        for prod in grammar.Productions do
            emptyNonterminals.Remove prod.Head |> ignore
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

        if grammar.Symbols.Terminals.Length > BuildError.SymbolLimit
            || grammar.Symbols.Nonterminals.Length > BuildError.SymbolLimit then
            do! Error BuildError.SymbolLimitExceeded
    }

    /// Performs some checks on the generated LALR and DFA tables.
    let private aPosterioriConsistencyCheck grammar (lalr: ImmutableArray<_>) (dfa: ImmutableArray<_>) = either {
        ignore grammar
        ignore lalr
        match dfa.[0].AcceptSymbol with
        | Some x -> do! Error <| BuildError.NullableSymbol x
        | None -> ()
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
                member __.Transform(term, context, data) = transformers.[int term.Index].Invoke(context, data)
                member __.Fuse(prod, members) = fusers.[int prod.Index].Invoke(members)
        }

    /// Creates a `Grammar` from a `GrammarDefinition`.
    [<CompiledName("BuildGrammarOnly")>]
    let buildGrammarOnly dg = either {
        do! aPrioriConsistencyCheck dg
        let! myDarlingLALRStateTable =
            LALRBuild.buildProductionsToLALRStates
                dg.StartSymbol
                dg.Symbols.Terminals
                dg.Symbols.Nonterminals
                dg.Productions
        let! mySweetDFAStateTable =
            DFABuild.buildRegexesToDFA
                true
                dg.Metadata.CaseSensitive
                dg.DFASymbols
        do! aPosterioriConsistencyCheck dg myDarlingLALRStateTable mySweetDFAStateTable
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

    /// Creates a `Grammar` and a `PostProcessor` from a typed `DesigntimeFarkle`.
    /// The construction of the grammar may fail. In this case, the output of the
    /// post-processor is indeterminate. Using this function (and all others in this
    /// module) will always build a new grammar, even if a precompiled one is available.
    [<CompiledName("Build")>]
    let build (df: DesigntimeFarkle<'TOutput>) =
        let myLovelyGrammarDefinition = createGrammarDefinition df
        let myFavoritePostProcessor = createPostProcessor<'TOutput> myLovelyGrammarDefinition
        let myDearestGrammar = buildGrammarOnly myLovelyGrammarDefinition
        myDearestGrammar, myFavoritePostProcessor

    /// Creates a `PostProcessor` from the given `DesigntimeFarkle`.
    /// By not creating a grammar, some potentially expensive steps are skipped.
    /// This function is useful only for some very limited scenarios, such as
    /// having many designtime Farkles with an identical grammar but different post-processors.
    [<CompiledName("BuildPostProcessorOnly")>]
    let buildPostProcessorOnly (df: DesigntimeFarkle<'TOutput>) =
        df |> createGrammarDefinition |> createPostProcessor<'TOutput>
