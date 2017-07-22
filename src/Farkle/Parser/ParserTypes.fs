// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar

type TokenData = TokenData of string
    with
        static member TokenData_ :Lens<_, _> = (fun (TokenData x) -> x), (fun v _ -> TokenData v)

type Token =
    {
        Symbol: Symbol
        Position: Position
        Data: TokenData
    }
    with
        static member Symbol_ :Lens<_, _> = (fun x -> x.Symbol), (fun v x -> {x with Symbol = v})
        static member Position_ :Lens<_, _> = (fun x -> x.Position), (fun v x -> {x with Position = v})
        static member Data_ :Lens<_, _> = (fun x -> x.Data), (fun v x -> {x with Data = v})

type Reduction =
    {
        Tokens: Token list
        Parent: Production
    }

type ParserState =
    {
        Grammar: Grammar
        InputStream: char list
        CurrentLALRState: LALRState
        TokenStack: Token list
        TrimReductions: bool
        CurrentPosition: Position
        GroupStack: Token list
    }
    with
        static member Grammar_ :Lens<_, _> = (fun x -> x.Grammar), (fun v x -> {x with Grammar = v})
        static member InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
        static member CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
        static member TokenStack_ :Lens<_, _> = (fun x -> x.TokenStack), (fun v x -> {x with TokenStack = v})
        static member TrimReductions_ :Lens<_, _> = (fun x -> x.TrimReductions), (fun v x -> {x with TrimReductions = v})
        static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
        static member GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})