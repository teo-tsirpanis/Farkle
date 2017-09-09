// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open System
open System.Text

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
    let ofReduction {Tokens = tokens; Parent = parent}: AST<Indexed<Production>> =
        let rec impl {Production.Index = prod} tokens =
            tokens
            |> List.map (Choice.tee2 (fun {Data = x} ->  Content (Indexed prod, x)) (fun {Parent = parent; Tokens = x} -> Nonterminal (Indexed prod, impl parent x)))
        Nonterminal (Indexed parent.Index, impl parent tokens)

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
        let kIndentText = "|  "
        let rec impl indent x = seq {
            let indentText = kIndentText |> Seq.replicate indent |> String.concat ""
            yield x |> metadata |> sprintf "%s+--%O" indentText
            match x with
            | Content (_, x) -> yield sprintf "%s%s+--%s" indentText kIndentText x
            | Nonterminal (_, x) ->
                for x in x do
                    yield! impl (indent + 1) x}
        impl 0 x |> String.concat Environment.NewLine