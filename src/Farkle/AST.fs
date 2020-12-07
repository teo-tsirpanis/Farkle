// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammar
open System.Text

/// A domain-ignorant Abstract Syntax Tree that describes the output of a parser.
[<RequireQualifiedAccess>]
type AST =
    | Content of Terminal: Terminal * Position: Position * Content: string
    | Nonterminal of Production: Production * Members: AST list
    override x.ToString() =
        match x with
        | Content (x, _, _) -> x.ToString()
        | Nonterminal (x, _) -> x.ToString()

/// Functions to work with `AST`s.
[<RequireQualifiedAccess>]
module AST =

    /// Simplifies an `AST` in the same fashion with GOLD Parser's "trim reductions" option.
    [<CompiledName("Simplify")>]
    let rec simplify x =
        match x with
        | AST.Content _ as x -> x
        | AST.Nonterminal (_, [x]) -> simplify x
        | AST.Nonterminal (prod, xs) -> AST.Nonterminal(prod, List.map simplify xs)

    /// Visualizes an `AST` in the form of a textual "parse tree".
    [<CompiledName("ToASCIITree")>]
    let toASCIITree x =
        let sb = StringBuilder()
        let rec impl indent x =
            for __ = 0 to indent do sb.Append("|  ") |> ignore
            sb.Append("+--") |> ignore
            match x with
            | AST.Content (_, _, data) -> sb.AppendLine(data) |> ignore
            | AST.Nonterminal (prod, x) ->
                sb.AppendLine(prod.ToString()) |> ignore
                x |> List.iter (impl <| indent + 1)
        impl 0 x
        sb.ToString()
