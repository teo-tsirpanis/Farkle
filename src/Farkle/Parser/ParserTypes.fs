// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar

type Token =
    {
        Symbol: Symbol
        Position: Position
        Data: string
    }
    with
        static member Symbol_ :Lens<_, _> = (fun x -> x.Symbol), (fun v x -> {x with Symbol = v})
        static member Position_ :Lens<_, _> = (fun x -> x.Position), (fun v x -> {x with Position = v})
        static member Data_ :Lens<_, _> = (fun x -> x.Data), (fun v x -> {x with Data = v})
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        static member AppendData data x = Optic.map Token.Data_ (fun x -> x + data) x

type Reduction =
    {
        Tokens: Token list
        Parent: Production
    }

module Reduction =

    let data {Tokens = tokens} =
        tokens
        |> List.map (Optic.get Token.Data_)
        |> String.concat ""

type ParseError =
    | IndexNotFound of uint16
    | GotoNotFoundAfterReduction
    | LALRStackEmpty

type LALRResult =
    | Accept
    | Shift
    | ReduceNormal
    | ReduceEliminated
    | SyntaxError of expected: Symbol list
    | InternalErrors of ParseError list

type ParseMessage =
    | TokenRead
    | Reduction
    | Accept
    | LexicalError of Token
    | SyntaxError of expected: Symbol list
    | GroupError
    | InternalErrors of ParseError list

type ParserState =
    {
        Grammar: Grammar
        InputStream: char list
        CurrentLALRState: LALRState
        InputStack: Token list
        LALRStack: (Token * (LALRState * Reduction option)) list
        TrimReductions: bool
        CurrentPosition: Position
        GroupStack: Token list
    }
    with
        static member grammar x = x.Grammar
        static member InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
        static member CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
        static member InputStack_ :Lens<_, _> = (fun x -> x.InputStack), (fun v x -> {x with InputStack = v})
        static member LALRStack_ :Lens<_, _> = (fun x -> x.LALRStack), (fun v x -> {x with LALRStack = v})
        static member trimReductions x = x.TrimReductions
        static member CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
        static member GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})