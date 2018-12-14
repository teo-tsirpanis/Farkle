// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammar
open System.Text

/// A token is an instance of a `Symbol`.
/// Tokens carry parsed data, as well as their position within the text file.
type Token =
    {
        /// The `Symbol` whose instance is this token.
        Symbol: Terminal
        /// The `Position` of the token in the input string.
        Position: Position
        /// The actual content of the token.
        Data: obj
    }
    with
        /// A shortcut for creating a token.
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        override x.ToString() = x.Data.ToString()

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
    let inline tee fContent fNonterminal =
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
        let addIndentText (x, indent) = String.replicate indent "|  " + x
        let sb = StringBuilder()
        let print = addIndentText >> sb.AppendLine >> ignore
        let rec impl indent x =
            match x with
            | AST.Content x -> print <| (sprintf "+--%O" x.Data, indent)
            | AST.Nonterminal (prod, x) ->
                print <| (sprintf "+--%O" prod, indent)
                x |> Seq.iter (impl <| indent + 1)
        impl 0 x
        sb.ToString()
