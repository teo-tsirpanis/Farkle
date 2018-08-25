// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Aether
open Farkle.Grammar
open System

/// A token is an instance of a `Symbol`.
/// Tokens carry parsed data, as well as their position within the text file.
type Token =
    {
        /// The `Symbol` whose instance is this token.
        Symbol: Symbol
        /// The `Position` of the token in the input string.
        Position: Position
        /// The actual content of the token.
        Data: string
    }
    with
        /// [omit]
        static member Symbol_ :Lens<_, _> = (fun x -> x.Symbol), (fun v x -> {x with Symbol = v})
        /// [omit]
        static member Position_ :Lens<_, _> = (fun x -> x.Position), (fun v x -> {x with Position = v})
        /// [omit]
        static member Data_ :Lens<_, _> = (fun x -> x.Data), (fun v x -> {x with Data = v})
        /// A shortcut for creating a token.
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        /// Returns a new token which has a string appended to its data.
        static member AppendData data x = Optic.map Token.Data_ (fun x -> x + data) x
        override x.ToString() = x.Data

module internal Token =

    let dummy sym = {Symbol = sym; Position = Position.initial; Data = ""}

/// An Abstract Syntax Tree that describes the output of a parser.
[<RequireQualifiedAccess>]
type AST =
    | Content of Token
    | Nonterminal of Production * AST list
    override x.ToString() =
        match x with
        | Content x -> x.Symbol.ToString()
        | Nonterminal (x, _) -> x.ToString()

/// Functions to work with `AST`s.
[<RequireQualifiedAccess>]
module AST =

    /// Maps an `AST` with either fContent or fNonterminal depending on what it is.
    [<CompiledName("Tee")>]
    let tee fContent fNonterminal =
        function
        | AST.Content x -> fContent x
        | AST.Nonterminal (x, y) -> fNonterminal (x, y)

    let internal headSymbols = tee (fun x -> x.Symbol.ToString()) (fst >> string)

    /// Simplifies an `AST` in the same fashion with GOLD Parser's "trim reductions" option.
    [<CompiledName("Simplify")>]
    let rec simplify x = tee AST.Content (function | (_, [x]) -> simplify x | (prod, x) -> AST.Nonterminal (prod, List.map simplify x)) x

    /// Visualizes an `AST` in the form of a textual "parse tree".
    [<CompiledName("ToASCIITree")>]
    let toASCIITree x =
        let addIndentText = String.repeat "|  "
        let rec impl indent x = seq {
            yield x |> headSymbols |> sprintf "+--%s", indent
            match x with
            | AST.Content x -> yield sprintf "+--%s" x.Data, indent
            | AST.Nonterminal (_, x) -> yield! x |> Seq.collect (impl <| indent + 1u)
        }
        impl 0u x |> Seq.map (fun (x, y) -> addIndentText y + x) |> String.concat Environment.NewLine
