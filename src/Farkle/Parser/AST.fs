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
    | Content of Symbol * string
    | Nonterminal of Production * AST list

/// Functions to work with `AST`s.
module AST =

    /// Creates an `AST` from a `Reduction`.
    /// The parent of the reduction is the tree's metadata.
    [<CompiledName("CreateFromReduction")>]
    let rec ofReduction {Tokens = tokens; Parent = parent} =
        let tokenToAST {Data = x} = Content (parent.Head, x)
        match tokens with
        | [Choice1Of2 x] -> tokenToAST x
        | tokens -> tokens |> List.map (Choice.tee2 tokenToAST ofReduction) |> (fun x -> Nonterminal (parent, x))

    /// Maps an `AST` with either fContent or fNonterminal depending on what it is.
    [<CompiledName("Tee")>]
    let tee fContent fNonterminal =
        function
        | Content (x, y) -> fContent (x, y)
        | Nonterminal (x, y) -> fNonterminal (x, y)

    let internal heads x = tee (fst >> string) (fst >> string) x

    /// Simplifies an `AST` in the same fashion with the "trim reductions" option.
    [<CompiledName("Simplify")>]
    let rec simplify x = tee Content (function | (_, [x]) -> simplify x | (prod, x) -> Nonterminal (prod, List.map simplify x)) x

    /// Visualizes an `AST` in the form of a textual "parse tree".
    [<CompiledName("DrawASCIITree")>]
    let drawASCIITree x =
        let addIndentText = String.repeat "|  "
        let rec impl indent x = seq {
            yield x |> heads |> sprintf "+--%s", indent
            match x with
            | Content (_, x) -> yield sprintf "+--%s" x, indent
            | Nonterminal (_, x) ->
                for x in x do
                    yield! impl (indent + 1u) x}
        impl 0u x |> Seq.map (fun (x, y) -> addIndentText y + x) |> String.concat Environment.NewLine

    /// Converts an `AST` to an arbitrary type.
    /// It accepts two functions.
    /// One converts a terminal symbol to an arbitrary object.
    /// The other "collapses" the many parts of a nonterminal symbol into _one_ arbitrary object.
    /// It is with the author's great sadness that this function is totally type-unsafe.
    /// This happens because these two functions handle all the possible types of symbols and productions respectively.
    // [<CompiledName("PostProcess")>]
    // let postProcess (fSymbol: _ -> _ -> obj option) fProduction x =
    //     let rec impl x =
    //         match x with
    //         | Content ((Terminal(index, _) as sym), x) -> fSymbol index x |> failIfNone (UnexpectedTerminal (sym, x))
    //         | Content (sym, _) -> sym |> UnexpectedSymbolTypeAsTerminal |> Result.Error
    //         | Nonterminal (prod, xs) -> xs |> List.map impl |> collect >>= (fProduction prod.Index >> failIfNone UnexpectedASTStructure)
    //     impl x
