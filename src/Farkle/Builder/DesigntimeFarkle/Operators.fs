// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Builder.ProductionBuilders
open Farkle.Common
open Farkle.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices

/// Functions to set metadata for designtime Farkles.
/// With few exceptions, these functions will have to be applied to the topmost
/// designtime Farkle that will get build, or they will have no effect.
/// Designime Farkles that were applied the functions of this module must not
/// be used with the original designtime Farkles in the same context; only
/// one grammar symbol will be created, with undefined behavior.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesigntimeFarkle =

    /// Gets the raw designtime Farkle without any wrappers it might have been under.
    let rec internal unwrap (df: DesigntimeFarkle) =
        match df with
        | :? DesigntimeFarkleWrapper as dfw -> unwrap dfw.InnerDesigntimeFarkle
        | _ -> df

    /// Gets an object representing the given designtime
    /// Farkle that can be used for equality checks.
    let internal getIdentityObject df =
        match unwrap df with
        | :? ITerminal as term -> term.IdentityObject
        | :? Literal as lit -> box lit.Content
        | dfUnwrapped -> box dfUnwrapped

    let internal getMetadata (df: DesigntimeFarkle) =
        match df with
        | :? DesigntimeFarkleWrapper as dfw -> dfw.Metadata
        | _ -> GrammarMetadata.Default

    let private withMetadataEx<'T when 'T :> DesigntimeFarkle> name metadata (df: 'T) : 'T =
        match box df with
        | :? IExposedAsDesigntimeFarkleChild as x -> x.WithMetadataSameType name metadata |> unbox
        | _ when typeof<'T> = typeof<DesigntimeFarkle> ->
            {new DesigntimeFarkleWrapper with
                member _.Name = name
                member _.Metadata = metadata
                member _.InnerDesigntimeFarkle = unwrap df} |> unbox
        | _ -> BuilderCommon.throwCustomDesigntimeFarkle()

    /// Sets a `GrammarMetadata` object to a designtime Farkle. Most
    /// other functions in this module are convenience wrappers over this function.
    let private withMetadata metadata df = df |> withMetadataEx df.Name metadata

    /// Sets an `OperatorScope` object to a designtime Farkle.
    /// This function can be applied in designtime Farkles that are not the
    /// topmost ones. Applying this function many times will discard the existing
    /// operator scope.
    let withOperatorScope opScope df =
        df |> withMetadata {getMetadata df with OperatorScope = opScope}

    /// Converts an untyped designtime Farkle to a typed one that returns an object.
    /// The object the designtime Farkle this function will return is undefined.
    /// This function is no longer useful for applying metadata to untyped designtime Farkles.
    let cast (df: DesigntimeFarkle): [<Nullable(1uy, 2uy)>] _ =
        match df with
        | :? DesigntimeFarkle<obj> as dfObj -> dfObj
        | _ -> DesigntimeFarkleWrapper<obj>(df.Name, getMetadata df, df) :> _

    /// Changes the name of a designtime Farkle. This function can be applied
    /// anywhere, not only to the topmost one, like with other metadata changes.
    let rename newName df =
        nullCheck (nameof newName) newName
        df |> withMetadataEx newName (getMetadata df)

    /// Sets the `CaseSensitive` field of a `DesigntimeFarkle`'s metadata.
    let caseSensitive flag df = df |> withMetadata {getMetadata df with CaseSensitive = flag}

    /// Sets the `AutoWhitespace` field of a `DesigntimeFarkle`'s metadata.
    let autoWhitespace flag df = df |> withMetadata {getMetadata df with AutoWhitespace = flag}

    /// Adds a name-`Regex` pair of noise symbols to the given `DesigntimeFarkle`.
    let addNoiseSymbol name regex df =
        nullCheck "name" name
        let metadata = getMetadata df
        df |> withMetadata {metadata with NoiseSymbols = metadata.NoiseSymbols.Add(name, regex)}

    let private addComment comment df =
        let metadata = getMetadata df
        df |> withMetadata {metadata with Comments = metadata.Comments.Add comment}

    /// Adds a line comment to the given `DesigntimeFarkle`.
    let addLineComment commentStart df =
        nullCheck "commentStart" commentStart
        addComment (LineComment commentStart) df

    /// Adds a block comment to the given `DesigntimeFarkle`.
    let addBlockComment commentStart commentEnd df =
        nullCheck "commentStart" commentStart
        nullCheck "commentEnd" commentEnd
        addComment (BlockComment(commentStart, commentEnd)) df

/// F# operators to easily work with designtime Farkles and production builders.
[<AutoOpen; CompiledName("FSharpDesigntimeFarkleOperators")>]
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

    /// An alias for the `Terminal.Virtual` function.
    let virtualTerminal name = Terminal.Virtual name

    /// An alias for the `Terminal.Literal` function.
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

    /// `ProductionBuilder.FinishConstant` as an operator.
    let (=%) (pb: ProductionBuilder) (x: 'T) = pb.FinishConstant(x)

    /// An alias for ``ProductionBuilder`1.AsIs``.
    let asIs (pb: ProductionBuilder<'T>) = pb.AsIs()

    /// An alias for the `WithPrecedence` method of production builders.
    let inline prec (token: obj) pb =
        (^TBuilder : (member WithPrecedence: obj -> ^TBuilder) (pb, token))

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

    /// Creates a designtime Farkle that recognizes many occurrences
    /// of the given one and returns them in any collection type.
    let manyCollection<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)> (df: DesigntimeFarkle<'T>) =
            let nont = sprintf "%s %s" df.Name typeof<'TCollection>.Name |> nonterminal

            nont.SetProductions(
                empty => (fun () -> new 'TCollection()),
                !@ nont .>>. df => (fun xs x -> (xs :> ICollection<_>).Add(x); xs)
            )

            nont :> DesigntimeFarkle<_>

    /// Creates a designtime Farkle that recognizes more than one occurrences
    /// of the given one and returns them in any collection type.
    let manyCollection1<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)> (df: DesigntimeFarkle<'T>) =
            let nont = sprintf "%s Non-empty %s" df.Name typeof<'TCollection>.Name |> nonterminal

            nont.SetProductions(
                !@ df => (fun x -> let xs = new 'TCollection() in xs.Add x; xs),
                !@ nont .>>. df => (fun xs x -> (xs :> ICollection<_>).Add(x); xs)
            )

            nont :> DesigntimeFarkle<_>

    /// Creates a designtime Farkle that recognizes many
    /// occurrences of the given one and returns them in a list.
    let many df =
        let dfBuilder = manyCollection df
        sprintf "%s List" df.Name ||= [(!@ dfBuilder).Finish ListBuilder<_>.MoveToListDelegate]

    /// Creates a designtime Farkle that recognizes more than one
    /// occurrences of the given one and returns them in a list.
    let many1 df =
        let dfBuilder = manyCollection1 df
        sprintf "%s Non-empty List" df.Name ||= [(!@ dfBuilder).Finish ListBuilder<_>.MoveToListDelegate]

    /// Creates a designtime Farkle that recognizes more than one occurrences
    /// of `df` separated by `sep` and returns them in any collection type.
    let sepByCollection1<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)>(sep: DesigntimeFarkle) (df: DesigntimeFarkle<'T>) =
        let nont =
            sprintf "%s Non-empty %s Separated By %s" df.Name typeof<'TCollection>.Name sep.Name |> nonterminal
        nont.SetProductions(
            !@ df => (fun x -> let xs = new 'TCollection() in xs.Add x; xs),
            !@ nont .>> sep .>>. df => (fun xs x -> (xs :> ICollection<_>).Add x; xs)
        )
        nont :> DesigntimeFarkle<_>

    /// Creates a designtime Farkle that recognizes many occurrences of
    /// `df` separated by `sep` and returns them in any collection type.
    let sepByCollection<'T, 'TCollection
        when 'TCollection :> ICollection<'T>
        and 'TCollection: (new: unit -> 'TCollection)>(sep: DesigntimeFarkle) (df: DesigntimeFarkle<'T>) =
        let nont =
            sprintf "%s %s Separated By %s" df.Name typeof<'TCollection>.Name sep.Name |> nonterminal
        nont.SetProductions(
            empty => (fun () -> new 'TCollection()),
            !@ (sepByCollection1 sep df) |> asIs
        )
        nont :> DesigntimeFarkle<_>

    /// Creates a designtime Farkle that recognizes more than one
    /// occurrences of `df` separated by `sep` and returns them in a list.
    let sepBy1 sep df =
        let dfBuilder = sepByCollection1 sep df
        sprintf "%s Non-empty List Separated By %s" df.Name sep.Name ||= [
            (!@ dfBuilder).Finish ListBuilder<_>.MoveToListDelegate
        ]

    /// Creates a designtime Farkle that recognizes many occurrences
    /// of `df` separated by `sep` and returns them in a list.
    let sepBy sep df =
        let dfBuilder = sepByCollection sep df
        sprintf "%s List Separated By %s" df.Name sep.Name ||= [
            (!@ dfBuilder).Finish ListBuilder<_>.MoveToListDelegate
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
