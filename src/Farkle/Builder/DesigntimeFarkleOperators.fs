// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
/// F# operators to easily work with productions and their builders.
module Farkle.Builder.DesigntimeFarkleOperators

open Farkle.Common
open System.Collections.Generic

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

let inline private dfName (df: DesigntimeFarkle) = df.Name

let private nonterminalf fmt df : string = (sprintf fmt (dfName df))

/// Like `|>>`, but allows setting a custom
/// name to the resulting `DesigntimeFarkle<T>`.
let mapEx label f df =
    label ||= [!@ df => f]

/// Creates a new `DesigntimeFarkle<'T>` that transforms
/// the output of the given one with the given function.
let (|>>) (f: _ -> 'b) df =
    let name = sprintf "%s :?> %s" (dfName df) typeof<'b>.Name
    mapEx name f df

/// Creates a `DesigntimeFarkle<'T>` that recognizes many
/// occurences of the given one and returns them in a list.
let many df =
    let nont = nonterminalf "%s List" df |> nonterminal
    nont.SetProductions(
        // A left-recursive design uses the LALR stack
        // more efficiently, but due to the nature of
        // F#'s cons list, we will make it right recursive, as
        // it avoids us an extra production that reverses the list.
        !@ df .>>. nont => (fun x xs -> x :: xs),
        empty =% []
    )
    nont :> DesigntimeFarkle<_>

/// Like `many1`, but requires at least one element to be present.
let many1 df =
    nonterminalf "%s Non-empty List" df
    ||= [!@ df .>>. many df => (fun x xs -> x :: xs)]

/// Like `many`, but returns the result in
/// any type that implements `ICollection<T>`.
let manyCollection<'T, 'TCollection
    when 'TCollection :> ICollection<'T>
    and 'TCollection: (new: unit -> 'TCollection)> (df: DesigntimeFarkle<'T>) =
        let nont = sprintf "%s %s" df.Name typeof<'TCollection>.Name |> nonterminal
        nont.SetProductions(
            empty => (fun () -> new 'TCollection()),
            !@ nont .>>. df => (fun xs x -> (xs :> ICollection<_>).Add(x); xs)
        )
        nont :> DesigntimeFarkle<_>

/// A combination of `many1` and `manyCollection`.
let manyCollection1 (df: DesigntimeFarkle<'T>): DesigntimeFarkle<'TCollection> =
    sprintf "%s Non-empty %s" df.Name typeof<'TCollection>.Name
    ||= [!@ (manyCollection df) .>>. df => (fun xs x -> xs.Add(x); xs)]

/// Like `sep`, but requires at least one element to be present.
let sepBy1 (sep: DesigntimeFarkle) df =
    let nont = nonterminalf "%s Non-empty List" df |> nonterminal
    nont.SetProductions(
        !@ df .>> sep .>>. nont => (fun x xs -> x :: xs),
        !@ df => List.singleton
    )
    nont :> DesigntimeFarkle<_>

/// Creates a `DesigntimeFarkle<T>` that recognizes
/// many occurences of `df` separated by `sep`.
let sepBy (sep: DesigntimeFarkle) df =
    nonterminalf "%s List" df
    ||= [
        !@ df .>> sep .>>. sepBy1 sep df => (fun x xs -> x :: xs)
        empty =% []
    ]

/// Creates a `DesigntimeFarkle<T>` that recognizes `df`,
/// which might not be found. In this case, the resulting
/// value is `None`.
let opt df =
    nonterminalf "%s Maybe" df
    ||= [
        !@ df => Some
        empty =% None
    ]
