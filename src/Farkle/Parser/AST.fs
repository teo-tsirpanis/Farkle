// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle
open Farkle.Grammar
open System

/// An Abstract Syntax Tree that describes the output of a parser.
/// It carries arbitrary metadata.
type AST<'TMetadata> =
    | Content of 'TMetadata * string
    | Nonterminal of 'TMetadata * AST<'TMetadata> list

/// Functions to work with `AST`s.
module AST =

    /// Creates an `AST` from a `Reduction`.
    /// The parent of the reduction is the tree's metadata.
    [<CompiledName("CreateFromReduction")>]
    let rec ofReduction {Tokens = tokens; Parent = parent} =
        let tokenToAST {Data = x} = Content (parent, x)
        match tokens with
        | [Choice1Of2 x] -> tokenToAST x
        | tokens -> tokens |> List.map (Choice.tee2 tokenToAST ofReduction) |> (fun x -> Nonterminal (parent, x))

    /// Maps an `AST` with either fContent or fNonterminal depending on what it is.
    [<CompiledName("Tee")>]
    let tee fContent fNonterminal =
        function
        | Content (x, y) -> fContent (x, y)
        | Nonterminal (x, y) -> fNonterminal (x, y)

    /// Returns the metadata of an `AST`.
    [<CompiledName("GetMetadata")>]
    let metadata x = tee fst fst x

    /// Simplifies an `AST` in the same fashion with the "trim reductions" option.
    [<CompiledName("Simplify")>]
    let rec simplify x = tee Content (function | (_, [x]) -> simplify x | (prod, x) -> Nonterminal (prod, List.map simplify x)) x

    /// Transforms the metadata of an `AST` with the given function.
    [<CompiledName("SelectMetadata")>]
    let rec mapMetadata f =
        function
        | Content (meta, x) -> Content (f meta, x)
        | Nonterminal (meta, x) -> Nonterminal (f meta, List.map (mapMetadata f) x)

    /// Visualizes an `AST` in the form of a textual "parse tree".
    [<CompiledName("DrawASCIITree")>]
    let drawASCIITree x =
        let addIndentText = String.repeat "|  "
        let rec impl indent x = seq {
            yield x |> metadata |> sprintf "+--%O", indent
            match x with
            | Content (_, x) -> yield sprintf "+--%s" x, indent
            | Nonterminal (_, x) ->
                for x in x do
                    yield! impl (indent + 1u) x}
        impl 0u x |> Seq.map (fun (x, y) -> addIndentText y + x) |> String.concat Environment.NewLine
