// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Common
open System
open System.Collections.Generic

[<AbstractClass; Sealed>]
/// A helper static class to create nonterminals.
type Nonterminal =
    /// <summary>Creates a <see cref="Nonterminal{T}"/> whose productions must be
    /// later set with <see cref="SetProductions"/>. Useful for recursive productions.</summary>
    /// <remarks>If the productions are not set, an error will be raised on building.</remarks>
    static member Create(name) =
        nullCheck "name" name
        {
            _Name = name
            Productions = SetOnce<_>.Create()
        }

    /// <summary>Creates a <see cref="DesigntimeFarkle{T}"/> that represents
    /// a nonterminal with a given name and productions.</summary>
    static member Create(name, firstProduction, [<ParamArray>] productions) =
        let nont = Nonterminal.Create name
        nont.SetProductions(firstProduction, productions)
        nont :> DesigntimeFarkle<_>

    /// <seealso cref="Farkle.Builder.Untyped.Nonterminal.Create" />
    static member CreateUntyped(name) = Untyped.Nonterminal.Create name

    /// <seealso cref="Farkle.Builder.Untyped.Nonterminal.Create" />
    static member CreateUntyped(name, firstProd, prods) =
        Untyped.Nonterminal.Create(name, firstProd, prods)

[<AbstractClass; Sealed>]
/// A helper static class to create groups. In Farkle (and GOLD parser),
/// groups are used to define lexical elements that start and
/// end with specified literals, and contain arbitrary characters.
/// Groups are a tokenizer's construct, and their content is
/// considered to be a terminal by the parser.
/// Comments are essentially groups, but this class
/// creates groups that have significant content.
type Group =
    /// <summary>Creates a line group. As the name says, it ends with a new line.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="fTransform">The function that transforms
    /// the group's position and data to <typeparamref name="T"/>. Must not be null.
    /// The given position is the position where <paramref name="groupStart"/> starts
    /// and the group's data do not include the new line that end it.</param>
    static member Line(name, groupStart, fTransform: T<'T>) =
        LineGroup(name, groupStart, T.box fTransform) :> DesigntimeFarkle<'T>
    /// <summary>Creates a block group. Block groups end with a string literal.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="groupEnd"> The sequence of characters
    /// that specify the end of the group.</param>
    /// <param name="fTransform">The function that transforms
    /// the group's position and data to <typeparamref name="T"/>. Must not be null.
    /// The given position is the position where <paramref name="groupStart"/> starts
    /// and the group's data do include <paramref name="groupEnd"/>.</param>
    static member Block(name, groupStart, groupEnd, fTransform: T<'T>) =
        BlockGroup(name, groupStart, groupEnd, T.box fTransform) :> DesigntimeFarkle<'T>
    /// <summary>Creates a line group that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    static member Line(name, groupStart) =
        nullCheck "name" name
        nullCheck "groupStart" groupStart
        {new AbstractLineGroup with
            member _.Name = name
            member _.Metadata = GrammarMetadata.Default
            member _.GroupStart = groupStart
            member _.Transformer = null} :> DesigntimeFarkle
    /// <summary>Creates a line group that does not contain any significant
    /// information for the parsing application.</summary>
    /// <param name="name">The group's name.</param>
    /// <param name="groupStart"> The sequence of characters
    /// that specify the beginning of the group.</param>
    /// <param name="groupEnd"> The sequence of characters
    /// that specify the end of the group.</param>
    static member Block(name, groupStart, groupEnd) =
        nullCheck "name" name
        nullCheck "groupStart" groupStart
        nullCheck "groupEnd" groupEnd
        {new AbstractBlockGroup with
            member _.Name = name
            member _.Metadata = GrammarMetadata.Default
            member _.GroupStart = groupStart
            member _.GroupEnd = groupEnd
            member _.Transformer = null} :> DesigntimeFarkle

[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
/// F# operators to easily work with designtime Farkles and production builders.
module DesigntimeFarkleOperators =

    /// Raises an error that happened during the parsing process.
    /// In contrast with raising an exception, these errors are caught
    /// by the `RuntimeFarkle` API and track their position.
    /// Use this function when the error might occur under normal circumstances
    /// (such as an unknown identifier name in a programming language).
    let error msg = raise(ParserApplicationException msg)

    /// An edition of `error` that supports formatted strings.
    let errorf fmt = Printf.ksprintf error fmt

    /// Creates a terminal with the given name, specified by the given `Regex`.
    /// Its content will be post-processed by the given `T` delegate.
    let terminal name fTransform regex = Terminal.Create(name, fTransform, regex)

    /// Creates a terminal with the given name,
    /// specified by the given `Regex`,
    /// but not returning anything.
    let terminalU name regex = Terminal.Create(name, regex)

    /// An alias for the `Terminal.NewLine` function.
    let literal str = Terminal.Literal str

    /// An alias for `Terminal.NewLine`.
    let newline = Terminal.NewLine

    /// Creates a `Nonterminal` whose productions must be
    /// later set with `SetProductions`, or it will raise an
    /// error on building. Useful for recursive productions.
    let nonterminal name = Nonterminal.Create name

    /// Creates an `Untyped.Nonterminal` whose productions must be
    /// later set with `SetProductions`, or it will raise an
    /// error on building. Useful for recursive productions.
    let nonterminalU name = Nonterminal.CreateUntyped(name)

    /// Creates a `DesigntimeFarkle&lt;'T&gt;` that represents
    /// a nonterminal with the given name and productions.
    let (||=) name members =
        match members with
        // There is no reason to throw an exception as in
        // the past. An error will occur sooner or later.
        | [] -> nonterminal name :> DesigntimeFarkle<_>
        | x :: xs -> Nonterminal.Create(name, x, Array.ofList xs)

    let (|||=) name members =
        match members with
        | [] -> nonterminalU name :> DesigntimeFarkle
        | (x: ProductionBuilder) :: xs -> Nonterminal.CreateUntyped(name, x, Array.ofList xs)

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
    let (=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)

    /// An alias for `ProductionBuilder.Empty`.
    let empty = ProductionBuilder.Empty

    /// Creates a production builder with one non-significant `DesigntimeFarkle`.
    /// This function is useful to start building a `Production`.
    let (!%) (df: DesigntimeFarkle) = empty.Append(df)

    /// Creates a production builder with one non-significant string literal.
    let (!&) str = empty.Append(str: string)

    /// Creates a production builder with one significant `DesigntimeFarkle&lt;'T&gt;`.
    /// This function is useful to start building a `Production`.
    let (!@) (df: DesigntimeFarkle<'T>) = empty.Extend(df)

    let inline private dfName (df: DesigntimeFarkle) = df.Name

    let private nonterminalf fmt df : string = (sprintf fmt (dfName df))

    /// Creates a new `DesigntimeFarkle&lt;'T&gt;` that transforms
    /// the output of the given one with the given function.
    let (|>>) df (f: _ -> 'b) =
        let name = sprintf "%s :?> %s" (dfName df) typeof<'b>.Name
        name ||= [!@ df => f]

    /// Creates a `DesigntimeFarkle&lt;'T&gt;` that recognizes many
    /// occurrences of the given one and returns them in a list.
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
        let nont = nonterminalf "%s Non-empty List" df |> nonterminal
        nont.SetProductions(
            !@ df .>>. nont => (fun x xs -> x :: xs),
            !@ df => List.singleton
        )
        nont :> DesigntimeFarkle<_>

    /// Like `many`, but returns the result in
    /// any type that implements `ICollection&lt;'T&gt;`.
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
    let manyCollection1<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)> (df: DesigntimeFarkle<'T>) =
            let nont = sprintf "%s Non-empty %s" df.Name typeof<'TCollection>.Name |> nonterminal

            nont.SetProductions(
                !@ df => (fun x -> let xs = new 'TCollection() in xs.Add x; xs),
                !@ nont .>>. df => (fun xs x -> (xs :> ICollection<_>).Add(x); xs)
            )

            nont :> DesigntimeFarkle<_>

    /// Like `sep`, but requires at least one element to be present.
    let sepBy1 (sep: DesigntimeFarkle) df =
        let nont = nonterminalf "%s Non-empty List" df |> nonterminal
        nont.SetProductions(
            !@ df .>> sep .>>. nont => (fun x xs -> x :: xs),
            !@ df => List.singleton
        )
        nont :> DesigntimeFarkle<_>

    /// Creates a `DesigntimeFarkle&lt;'T&gt;` that recognizes
    /// many occurences of `df` separated by `sep`.
    let sepBy (sep: DesigntimeFarkle) df =
        nonterminalf "%s List" df
        ||= [
            !@ df .>> sep .>>. sepBy1 sep df => (fun x xs -> x :: xs)
            empty =% []
        ]

    /// Creates a `DesigntimeFarkle&lt;'T&gt;` that recognizes `df`,
    /// which might not be found. In this case, the resulting
    /// value is `None`.
    let opt df =
        nonterminalf "%s Maybe" df
        ||= [
            !@ df => Some
            empty =% None
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Functions to set metadata for designtime Farkles.
/// Keep in mind that apart from renames, only the metadata of the _topmost_
/// designtime Farkle matter. Any other metadata changes will be disregarded.
module DesigntimeFarkle =

    /// Sets a custom `GrammarMetadata` object to a `DesigntimeFarkle&lt;'T&gt;`.
    let withMetadata metadata df =
        {DesigntimeFarkleWrapper.Create df with Metadata = metadata} :> DesigntimeFarkle<_>

    /// Converts an untyped designtime Farkle to a typed one that accepts an object.
    /// This is the recommended way to apply metadata to untyped designtime Farkles.
    /// The object they will return is typically null, but it should not be taken for granted.
    /// After the metadata have been set, it is better to upcast back to an untyped one.
    let cast (df: DesigntimeFarkle) =
        match df with
        | :? DesigntimeFarkle<obj> as dfObj -> dfObj
        | _ -> upcast {InnerDesigntimeFarkle = df; Name = df.Name; Metadata = df.Metadata}

    /// Changes the name of a designtime Farkle. This function can be applied
    /// anywhere, not only to the topmost one, like with other metadata changes.
    /// Using the same designtime Farkle with a different name will create only
    /// one grammar symbol whose name is undefined.
    let rename newName df =
        nullCheck "newName" newName
        {DesigntimeFarkleWrapper.Create df with Name = newName} :> DesigntimeFarkle<_>

    /// Sets the `CaseSensitive` field of a `DesigntimeFarkle`'s metadata.
    let caseSensitive flag df = df |> withMetadata {df.Metadata with CaseSensitive = flag}

    /// Sets the `AutoWhitespace` field of a `DesigntimeFarkle`'s metadata.
    let autoWhitespace flag df = df |> withMetadata {df.Metadata with AutoWhitespace = flag}

    /// Adds a name-`Regex` pair of noise symbols to the given `DesigntimeFarkle`.
    let addNoiseSymbol name regex df =
        nullCheck "name" name
        df |> withMetadata {df.Metadata with NoiseSymbols = df.Metadata.NoiseSymbols.Add(name, regex)}

    let private addComment comment df =
        df |> withMetadata {df.Metadata with Comments = df.Metadata.Comments.Add comment}

    /// Adds a line comment to the given `DesigntimeFarkle`.
    let addLineComment commentStart df =
        nullCheck "commentStart" commentStart
        addComment (LineComment commentStart) df

    /// Adds a block comment to the given `DesigntimeFarkle`.
    let addBlockComment commentStart commentEnd df =
        nullCheck "commentStart" commentStart
        nullCheck "commentEnd" commentEnd
        addComment (BlockComment(commentStart, commentEnd)) df
