// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar
open System

/// An Abstract Syntax Tree that describes the output of a parser.
type AST =
    | Content of Token
    | Nonterminal of Production * AST list

/// Functions to work with `AST`s.
module AST =

    /// Creates an `AST` from a `Reduction`.
    [<CompiledName("CreateFromReduction"); Obsolete("Reductions are gonna be shown the door.")>]
    let ofReduction x =
        let rec impl {Tokens = tokens; Parent = parent} =
            match tokens with
            | [Choice1Of2 x] -> Content x
            | tokens -> tokens |> List.map (Choice.tee2 Content impl) |> (fun x -> Nonterminal (parent, x))
        impl x

    /// Maps an `AST` with either fContent or fNonterminal depending on what it is.
    [<CompiledName("Tee")>]
    let tee fContent fNonterminal =
        function
        | Content x -> fContent x
        | Nonterminal (x, y) -> fNonterminal (x, y)

    let internal headSymbols x =
        match x with
        | Content x -> x.Symbol.ToString()
        | Nonterminal (x, _) -> x.ToString()

    /// Simplifies an `AST` in the same fashion with GOLD Parser's "trim reductions" option.
    [<CompiledName("Simplify")>]
    let rec simplify x = tee Content (function | (_, [x]) -> simplify x | (prod, x) -> Nonterminal (prod, List.map simplify x)) x

    /// Visualizes an `AST` in the form of a textual "parse tree".
    [<CompiledName("DrawASCIITree")>]
    let drawASCIITree x =
        let addIndentText = String.repeat "|  "
        let rec impl indent x = seq {
            yield x |> headSymbols |> sprintf "+--%s", indent
            match x with
            | Content x -> yield sprintf "+--%s" x.Data, indent
            | Nonterminal (_, x) -> yield! x |> Seq.collect (impl <| indent + 1u)
        }
        impl 0u x |> Seq.map (fun (x, y) -> addIndentText y + x) |> String.concat Environment.NewLine
