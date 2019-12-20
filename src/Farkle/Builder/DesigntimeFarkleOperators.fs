// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
/// F# operators to easily work with productions and their builders.
module Farkle.Builder.DesigntimeFarkleOperators

open Farkle.Common

/// Creates a terminal with the given name, specified by the given `Regex`.
/// Its content will be post-processed by the given `T` delegate.
let inline terminal name fTransform regex = Terminal.Create(name, fTransform, regex)

/// Creates an untyped `DesigntimeFarkle` that recognizes a literal string
let literal str = Literal str :> DesigntimeFarkle

/// An alias for `Terminal.NewLine`.
let newline = Terminal.NewLine

/// Creates a `Nonterminal` whose productions must be
/// set with `SetProductions`, or it will raise an
/// error. Useful for recursive productions.
let nonterminal name = {
    _Name = name
    Productions = SetOnce<_>.Create()
}

/// Creates a `DesigntimeFarkle<'T>` that represents
/// a nonterminal with the given name and productions.
let (||=) name members =
    let nont = nonterminal name
    match members with
    // There is no reason to throw an exception as in
    // the past. An error will occur sooner or later.
    | [] -> ()
    | x :: xs -> nont.SetProductions(x, Array.ofList xs)
    nont :> DesigntimeFarkle<_>

/// The `Append` method of production builders as an operator.
// https://github.com/ionide/ionide-vscode-fsharp/issues/1203
let inline op_DotGreaterGreater pb df =
    (^TBuilder : (member Append: ^TDesigntimeFarkle -> ^TBuilder) (pb, df))

/// The `Extend` method of production builders as an operator.
let inline op_DotGreaterGreaterDot pb df =
    (^TBuilder : (member Extend: DesigntimeFarkle<'T> -> ^TBuilderResult) (pb, df))

/// The `Finish` method of production builders as an operator.
let inline (=>) pb f =
    (^TBuilder : (member FinishFSharp: ^TFunction -> Production<'T>) (pb, f))

/// `ProductionBuilder.FinishConstant` as an operator.
let inline (=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)

/// A production builder with no members.
let empty = ProductionBuilder.Empty

/// Creates a production builder with one non-significant `DesigntimeFarkle`.
/// This function is useful to start building a `Production`.
let inline (!%) (df: DesigntimeFarkle) = empty.Append(df)

/// Creates a production builder with one non-significant string literal.
let inline (!&) str = empty.Append(str: string)

/// Creates a production builder with one significant `DesigntimeFarkle<'T>`.
/// This function is useful to start building a `Production`.
let inline (!@) (df: DesigntimeFarkle<'T>) = empty.Extend(df)
