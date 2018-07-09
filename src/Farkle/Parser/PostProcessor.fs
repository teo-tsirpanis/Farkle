// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open Farkle.Grammar
open System

/// An error in the post-processor.
type PostProcessError =
    | TerminalPostProcessorTypeMismatch of expected: Type * actual:Type
    | UnexpectedASTStructure
    | UnexpectedSymbolTypeAsTerminal of Symbol
    | UnknownTerminalSymbol of Symbol

type Transformer<'T> = internal {
    AcceptingSymbol: uint32
    TheTransformer: string -> 'T
}

type internal GeneralizedTransformer = {
    AcceptingSymbol': uint32
    OutputType': Type
    TheTransformer': string -> obj
}
with
    static member Transform x {TheTransformer' = trans; OutputType' = typ} =
        let res = trans x
        match res.GetType() with
        | t when t = typ -> Ok res
        | t -> (typ, t) |> TerminalPostProcessorTypeMismatch |> fail

type TerminalPostProcessor = internal {
    Transformers: Map<uint32,GeneralizedTransformer>
}
with
    member x.PostProcess sym data =
        match sym with
        | Terminal (index, _) ->
            x.Transformers.TryFind index
            |> failIfNone (UnknownTerminalSymbol sym)
            >>= (GeneralizedTransformer.Transform data)
        | sym -> sym |> UnexpectedSymbolTypeAsTerminal |> fail

module Transformer =

    let map f x =
        {
            AcceptingSymbol = x.AcceptingSymbol
            TheTransformer = x.TheTransformer >> f
        }

    let internal generalize (m: Transformer<'T>) =
        {
            AcceptingSymbol' = m.AcceptingSymbol
            OutputType' = typeof<'T>
            TheTransformer' = m.TheTransformer >> box
        }
    