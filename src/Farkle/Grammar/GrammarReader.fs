// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle.Common
open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser.EGTReader
open Farkle.Monads.Either
open System
open System.Collections.Immutable

module internal GrammarReader =

    module private Implementation =

        type AnySymbol =
            | AnyTerminal of Terminal
            | AnyNonterminal of Nonterminal
            | AnyEndOfFile
            | AnyNoise of Noise
            | AnyGroupStart of GroupStart
            | AnyGroupEnd of GroupEnd
            | AnyError

        let createSafeIndexed<'a> (arr: ImmutableArray.Builder<'a>) idx =
            if int idx <= arr.Capacity then
                idx |> uint32
            else
                invalidEGT()

        let wantTerminal x = match x with | AnyTerminal x -> x | _ -> invalidEGT()
        let wantNonterminal x = match x with | AnyNonterminal x -> x | _ -> invalidEGT()
        let wantProductionHandle name x =
            match x with
            | AnyTerminal x -> LALRSymbol.Terminal x
            | AnyNonterminal x -> LALRSymbol.Nonterminal x
            | AnyGroupEnd _ -> raise <| ProductionHasGroupEndException name
            | _ -> invalidEGT()
        let wantNoise x = match x with | AnyNoise x -> x | _ -> invalidEGT()
        let wantContainer x = match x with | AnyTerminal x -> Choice1Of2 x | AnyNoise x -> Choice2Of2 x | _ -> invalidEGT()
        let wantGroupStart x = match x with | AnyGroupStart x -> x | _ -> invalidEGT()
        let wantGroupEnd x =
            match x with
            | AnyTerminal x when x.Name.Equals("NewLine", StringComparison.OrdinalIgnoreCase) -> Choice1Of3 x
            | AnyNoise x when x.Name.Equals("NewLine", StringComparison.OrdinalIgnoreCase) -> Choice2Of3 x
            | AnyGroupEnd x -> Choice3Of3 x
            | _ -> invalidEGT()
        let wantDFASymbol x = match x with | AnyTerminal x -> Choice1Of4 x | AnyNoise x -> Choice2Of4 x | AnyGroupStart x -> Choice3Of4 x | AnyGroupEnd x -> Choice4Of4 x | _ -> invalidEGT()

        let wantAdvanceMode x idx = match wantUInt16 x idx with | 0us -> AdvanceMode.Token | 1us -> AdvanceMode.Character | _ -> invalidEGT()
        let wantEndingMode x idx = match wantUInt16 x idx with | 0us -> EndingMode.Open | 1us -> EndingMode.Closed | _ -> invalidEGT()

        let readProperty mem =
            lengthMustBe mem 3
            wantString mem 1, wantString mem 2

        let readCharSet _index mem =
            lengthMustBeAtLeast mem 3
            let _unicodePlane = wantUInt16 mem 0
            let count = wantUInt16 mem 1 |> int
            wantEmpty mem 2
            lengthMustBe mem (3 + 2 * count)
            let rangesSpan = mem.Slice(3)
            let wantChar idx = wantUInt16 rangesSpan idx |> char
            Array.init count (fun idx -> wantChar <| 2 * idx, wantChar <| 2 * idx + 1)

        let defaultGroupIndex = UInt32.MaxValue // This is impossible to occur in a grammar file; it only goes up to 65536.

        let readSymbol index mem =
            lengthMustBe mem 2
            let name = wantString mem 0
            let kind = wantUInt16 mem 1
            match kind with
            | 0us -> Nonterminal(index, name) |> AnyNonterminal
            | 1us -> Terminal(index, name) |> AnyTerminal
            | 2us -> Noise name |> AnyNoise
            | 3us -> AnyEndOfFile
            | 4us -> GroupStart(name, defaultGroupIndex) |> AnyGroupStart
            | 5us -> GroupEnd name |> AnyGroupEnd
            | 7us -> AnyError
            | _ -> invalidEGT()

        let readGroup (symbols: ImmutableArray.Builder<_>) fSymbol fGroup index mem =
            lengthMustBeAtLeast mem 7
            let name = wantString mem 0
            let container = wantUInt16 mem 1 |> fSymbol |> wantContainer
            let startSymbol =
                let startIdx = wantUInt16 mem 2
                let (GroupStart(name, _)) = startIdx |> fSymbol |> wantGroupStart
                let newSymbol = GroupStart(name, index)
                symbols.[int startIdx] <- AnyGroupStart newSymbol
                newSymbol
            let endSymbol = wantUInt16 mem 3 |> fSymbol |> wantGroupEnd
            let advanceMode = wantAdvanceMode mem 4
            let endingMode = wantEndingMode mem 5
            wantEmpty mem 6
            let nesting =
                let count = wantUInt16 mem 7 |> int
                lengthMustBe mem (8 + count)
                let span = mem.Slice(8)
                if not span.IsEmpty then
                    Seq.init mem.Length (wantUInt16 span >> fGroup) |> ImmutableHashSet.CreateRange
                else
                    ImmutableHashSet.Empty
            {
                    Name = name
                    ContainerSymbol = container
                    Start = startSymbol
                    End = endSymbol
                    AdvanceMode = advanceMode
                    EndingMode = endingMode
                    Nesting = nesting
            }

        let readProduction fSymbol index mem =
            lengthMustBeAtLeast mem 2
            let headSymbol = wantUInt16 mem 0 |> fSymbol |> wantNonterminal
            wantEmpty mem 1
            let symbols =
                let mem = mem.Slice(2)
                Seq.init mem.Length (wantUInt16 mem >> fSymbol >> wantProductionHandle index) |> ImmutableArray.CreateRange
            {Index = index; Head = headSymbol; Handle = symbols}

        let readInitialStates fDFA fLALR mem =
            lengthMustBe mem 2
            let dfa = wantUInt16 mem 0 |> fDFA |> int
            let lalr = wantUInt16 mem 1 |> fLALR |> int
            dfa, lalr

        let readDFAState fCharSet fSymbol fDFA index mem =
            lengthMustBeAtLeast mem 3
            let isAcceptState = wantBoolean mem 0
            let acceptIndex = wantUInt16 mem 1
            wantEmpty mem 2
            let edges =
                let states = mem.Slice(3)
                if states.Length % 3 <> 0 then invalidEGT()
                let edgesLength = states.Length / 3
                let fEdge idx =
                    let charSet = wantUInt16 states <| 3 * idx |> fCharSet
                    let target = wantUInt16 states <| 3 * idx + 1 |> fDFA
                    wantEmpty states <| 3 * idx + 2
                    charSet, target
                Array.init edgesLength fEdge |> RangeMap.ofRanges |> Option.defaultWith invalidEGT
            let acceptSymbol =
                if isAcceptState then
                    fSymbol acceptIndex |> wantDFASymbol |> Some
                else
                    None
            {Index = index; AcceptSymbol = acceptSymbol; Edges = edges}

        let readLALRState fSymbol fProduction fLALR index mem =
            lengthMustBeAtLeast mem 5 // There must be at least one action per state.
            wantEmpty mem 0
            let SRActions = ImmutableDictionary.CreateBuilder()
            let GotoActions = ImmutableDictionary.CreateBuilder()
            let mutable EOFAction = None
            let mem = mem.Slice(1)
            if mem.Length % 4 <> 0 then invalidEGT()
            let fAction idx =
                let symbolIndex = wantUInt16 mem <| 4 * idx
                let symbol = fSymbol symbolIndex
                let action = wantUInt16 mem <| 4 * idx + 1
                let targetIndex = wantUInt16 mem <| 4 * idx + 2
                wantEmpty mem <| 4 * idx + 3
                if action = 3us then
                    GotoActions.Add(wantNonterminal symbol, fLALR targetIndex)
                else
                    let srAction =
                        match action with
                        | 1us -> LALRAction.Shift <| fLALR targetIndex
                        | 2us -> LALRAction.Reduce <| fProduction targetIndex
                        | 4us -> LALRAction.Accept
                        | _ -> invalidEGT()
                    match symbol with
                    | AnyTerminal term -> SRActions.Add(term, srAction)
                    | AnyEndOfFile when EOFAction.IsNone -> EOFAction <- Some srAction
                    | _ -> invalidEGT()
            for i = 0 to mem.Length / 4 - 1 do fAction i
            {Index = index; Actions = SRActions.ToImmutable(); EOFAction = EOFAction; GotoActions = GotoActions.ToImmutable()}

        let itemTry (arr: ImmutableArray.Builder<_>) idx =
            let idx = int idx
            if idx < arr.Count then
                arr.[idx]
            else
                invalidEGT()

        let readAndAssignIndexed fRead (arr: ImmutableArray.Builder<_>) mem =
            lengthMustBeAtLeast mem 1
            let index = wantUInt16 mem 0
            let content = mem.Slice(1)
            content |> fRead (uint32 index) |> arr.Add

        let inline changeOnce x newValue =
            match !x with
            | None -> x := Some newValue
            | Some _ -> invalidEGT()

        [<Literal>]
        let CGTHeader = "GOLD Parser Tables/v1.0"
        [<Literal>]
        let EGTHeader = "GOLD Parser Tables/v5.0"

        let headerCheck =
            function
            | CGTHeader -> Error ReadACGTFile
            | EGTHeader -> Ok ()
            | _ -> Error InvalidEGTFile

    open Implementation

    let read r =
        let mutable isTableCountsInitialized = false
        let mutable hasReadAnyGroup = false
        let initialStates = ref None
        let properties = ImmutableDictionary.CreateBuilder()
        let mutable charSets = Unchecked.defaultof<_>
        let mutable fCharSet = Unchecked.defaultof<_>
        let mutable symbols = Unchecked.defaultof<_>
        let mutable fSymbol = Unchecked.defaultof<_>
        let mutable groups = Unchecked.defaultof<_>
        let mutable fGroupIndex = Unchecked.defaultof<_>
        let mutable productions = Unchecked.defaultof<_>
        let mutable fProduction = Unchecked.defaultof<_>
        let mutable dfaStates = Unchecked.defaultof<_>
        let mutable fDFAIndex = Unchecked.defaultof<_>
        let mutable lalrStates = Unchecked.defaultof<_>
        let mutable fLALRIndex = Unchecked.defaultof<_>
        let terminals = ImmutableArray.CreateBuilder()
        let nonterminals = ImmutableArray.CreateBuilder()
        let noiseSymbols = ImmutableArray.CreateBuilder()
        let fRecord mem =
            lengthMustBeAtLeast mem 1
            let magicCode = wantByte mem 0
            let mem = mem.Slice(1)
            match magicCode with
            | 'p'B ->
                let name, value = readProperty mem
                properties.Add(name, value)
            // The table counts record must exist only once, and before the other records.
            | 't'B when not isTableCountsInitialized ->
                isTableCountsInitialized <- true
                lengthMustBe mem 6
                let createB idx = wantUInt16 mem idx |> int |> ImmutableArray.CreateBuilder
                symbols <- createB 0
                fSymbol <- itemTry symbols
                charSets <- createB 1
                fCharSet <- itemTry charSets
                productions <- createB 2
                fProduction <- itemTry productions
                dfaStates <- createB 3
                fDFAIndex <- createSafeIndexed dfaStates
                lalrStates <- createB 4
                fLALRIndex <- createSafeIndexed lalrStates
                groups <- createB 5
                fGroupIndex <- createSafeIndexed groups
            | 'c'B when isTableCountsInitialized ->
                readAndAssignIndexed readCharSet charSets mem
            | 'S'B when isTableCountsInitialized && not hasReadAnyGroup ->
                let index = wantUInt16 mem 0
                let symbol = mem.Slice(1) |> readSymbol (uint32 index)
                symbols.Add symbol
                match symbol with
                | AnyTerminal x -> terminals.Add x
                | AnyNonterminal x -> nonterminals.Add x
                | AnyNoise x -> noiseSymbols.Add x
                | _ -> ()
            | 'g'B when isTableCountsInitialized ->
                hasReadAnyGroup <- true
                readAndAssignIndexed (readGroup symbols fSymbol fGroupIndex) groups mem
            | 'R'B when isTableCountsInitialized ->
                readAndAssignIndexed (readProduction fSymbol) productions mem
            | 'I'B when isTableCountsInitialized ->
                readInitialStates fDFAIndex fLALRIndex mem
                |> changeOnce initialStates
            | 'D'B when isTableCountsInitialized ->
                readAndAssignIndexed (readDFAState fCharSet fSymbol fDFAIndex) dfaStates mem
            | 'L'B when isTableCountsInitialized ->
                readAndAssignIndexed (readLALRState fSymbol fProduction fLALRIndex) lalrStates mem
            | _ -> invalidEGT()
        either {
            do! readEGT headerCheck fRecord r
            let! (initialDFA, initialLALR) = !initialStates |> failIfNone InvalidEGTFile
            let symbols = {
                Terminals = terminals.ToImmutable()
                Nonterminals = nonterminals.ToImmutable()
                NoiseSymbols = noiseSymbols.ToImmutable()
            }
            let dfaStates = dfaStates.MoveToImmutable()
            let lalrStates = lalrStates.MoveToImmutable()
            let! startSymbol =
                lalrStates.[initialLALR].GotoActions
                |> Seq.tryPick(fun x ->
                    match lalrStates.[int x.Value].EOFAction with
                    | Some LALRAction.Accept -> Some <| Ok x.Key
                    | _ -> None)
                |> Option.defaultValue (Error InvalidEGTFile)
            return {
                _Properties = properties.ToImmutable()
                _StartSymbol = startSymbol
                _Symbols = symbols
                _Productions = productions.MoveToImmutable()
                _Groups = groups.MoveToImmutable()
                _LALRStates = {InitialState = lalrStates.[initialLALR]; States = lalrStates}
                _DFAStates = {InitialState = dfaStates.[initialDFA]; States = dfaStates}
            }
        }
