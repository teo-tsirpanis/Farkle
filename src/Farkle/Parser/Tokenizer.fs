// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Aether.Operators
open Farkle
open Farkle.Grammar
open Farkle.Monads

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Tokenizer =

    type private TokenizerState =
        {
            InputStream: char list
            CurrentPosition: Position
            GroupStack: Token list
        }
        with
            static member InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
            static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
            static member GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})
            static member Create input = {InputStream = input; CurrentPosition = Position.initial; GroupStack = []}

    open State

    let private getLookAheadBuffer n x =
        let n = System.Math.Min(int n, List.length x)
        x |> List.takeSafe n |> String.ofList

    let rec private consumeBuffer n = state {
        let consumeSingle = state {
            let! x = getOptic TokenizerState.InputStream_
            match x with
            | x :: xs ->
                do! setOptic TokenizerState.InputStream_ xs
                match x with
                | LF ->
                    let! c = getOptic TokenizerState.CurrentPosition_ <!> Position.column
                    if c > 1u then
                        do! mapOptic TokenizerState.CurrentPosition_ Position.incLine
                | CR -> do! mapOptic TokenizerState.CurrentPosition_ Position.incLine
                | _ -> do! mapOptic TokenizerState.CurrentPosition_ Position.incCol
            | [] -> do ()
        }
        match n with
        | 0u -> do ()
        | n ->
            do! consumeSingle
            do! consumeBuffer (n - 1u)
    }

    // Pascal code (ported from Java 💩): 72 lines of begin/ends, mutable hell and unreasonable garbage.
    // F# code: 22 lines of clear, reasonable and type-safe code. I am so confident and would not even test it!
    // This is a 30.5% decrease of code and a 30.5% increase of productivity. Why do __You__ still code in C# (☹)? Or Java (😠)?
    let private tokenizeDFA {Transition = trans; InitialState = initialState; AcceptStates = accStates} {CurrentPosition = pos; InputStream = input} =
        let newToken = Token.Create pos
        let rec impl currPos currState lastAccept lastAccPos x =
            let newPos = currPos + 1u
            match x with
            | [] ->
                match lastAccept with
                | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x
                | None -> newToken EndOfFile ""
            | x :: xs ->
                let newDFA =
                    trans.TryFind(currState)
                    |> Option.bind (fun m -> m |> Map.toSeq |> Seq.tryFind (fun (cs, _) -> RangeSet.contains cs x))
                    |> Option.map snd
                match newDFA with
                | Some dfa ->
                    match accStates.TryFind(dfa) with
                    | Some sym -> impl newPos dfa (Some sym) currPos xs
                    | None -> impl newPos dfa lastAccept lastAccPos xs
                | None ->
                    match lastAccept with
                    | Some x -> input |> getLookAheadBuffer lastAccPos |> newToken x
                    | None -> input |> getLookAheadBuffer 1u |> newToken Error
        impl 1u initialState None 0u input

    let private produceToken dfa groups = state {
        let rec impl() = state {
            let! x = get <!> (tokenizeDFA dfa)
            let! groupStackTop = getOptic TokenizerState.GroupStack_ <!> List.tryHead
            let nestGroup =
                match x.Symbol with
                | GroupStart _ | GroupEnd _ ->
                    Maybe.maybe {
                        let! groupStackTop = groupStackTop
                        let! gsTopGroup = groupStackTop.Symbol |> Group.getSymbolGroup groups
                        let! myIndex = x.Symbol |> Group.getSymbolGroupIndexed groups
                        return gsTopGroup.Nesting.Contains myIndex
                    } |> Option.defaultValue true
                | _ -> false
            if nestGroup then
                do! x.Data |> String.length |> consumeBuffer
                let newToken = Optic.set Token.Data_ "" x
                do! mapOptic TokenizerState.GroupStack_ (List.cons newToken)
                return! impl()
            else
                match groupStackTop with
                | None ->
                    do! x.Data |> String.length |> consumeBuffer
                    return x
                | Some groupStackTop ->
                    let groupStackTopGroup =
                        groupStackTop.Symbol
                        |> Group.getSymbolGroup groups
                        |> mustBeSome // I am sorry. 😭
                    if groupStackTopGroup.EndSymbol = x.Symbol then
                        let! pop = state {
                            do! mapOptic TokenizerState.GroupStack_ List.tail
                            if groupStackTopGroup.EndingMode = Closed then
                                do! x.Data |> String.length |> consumeBuffer
                                return groupStackTop |> Token.AppendData x.Data
                            else
                                return groupStackTop
                        }
                        let! groupStackTop = getOptic (TokenizerState.GroupStack_ >-> List.head_)
                        match groupStackTop with
                            | Some _ ->
                                do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData pop.Data)
                                return! impl()
                            | None -> return Optic.set Token.Symbol_ groupStackTopGroup.ContainerSymbol pop
                    elif x.Symbol = EndOfFile then
                        return x
                    else
                        match groupStackTopGroup.AdvanceMode with
                        | Token ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (Token.AppendData x.Data)
                            do! x.Data |> String.length |> consumeBuffer
                        | Character ->
                            do! mapOptic (TokenizerState.GroupStack_ >-> List.head_) (x.Data.[0] |> string |> Token.AppendData)
                            do! consumeBuffer 1u
                        return! impl()
        }
        let! token = impl()
        let! isGroupStackEmpty = getOptic TokenizerState.GroupStack_ <!> List.isEmpty
        let! currentPosition = getOptic TokenizerState.CurrentPosition_
        return {NewToken = token; IsGroupStackEmpty = isGroupStackEmpty; CurrentPosition = currentPosition}
    }

    let create dfa groups input: Tokenizer = lazy (EndlessProcess.ofState (produceToken dfa groups) (TokenizerState.Create input))
