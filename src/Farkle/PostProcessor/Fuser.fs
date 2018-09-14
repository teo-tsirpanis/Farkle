// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle
open System

/// This type contains the logic to combine multiple symbols of one production into one arbitrary object.
/// These symbols are either transformed by a `Transformer` if they are terminals,
/// or, if they are productions, are the products of previous fusers.
type Fuser = internal {
    InputTypes: Type list
    OutputType: Type
    TheFuser: obj list -> obj
}
with
    /// Fuses the many post-processed parts of a production into one.
    /// The types of the objects are optionally checked, depending on the kind of the fuser in question.
    static member Fuse x fus =
        let typeCheck =
            List.zip fus.InputTypes x
            |> List.forall (fun (x1, x2) -> x1.IsAssignableFrom(x2.GetType()))
        if typeCheck then
            fus.TheFuser x |> Ok
        else
            UnexpectedASTStructure |> Error

/// Functions to create `Fuser`s.
module Fuser =
    /// Creates a `Fuser` that ~~fuses~~ maps a single-symbol production.
    let create1 (fFuser: 'd -> 'r) =
        {
            InputTypes = [typeof<'d>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> x.[0] :?> 'd |> fFuser |> box)
        }

    /// Creates a `Fuser` that takes a production with one item and returns it unmodified.
    let identity = create1 id

    /// Creates a `Fuser` that fuses a production with two symbols.
    let create2 (fFuser: 'd1 -> 'd2 -> 'r) =
        {
            InputTypes = [typeof<'d1>; typeof<'d2>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) |> box)
        }

    /// Creates a `Fuser` that fuses a production with three symbols.
    let create3 (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        {
            InputTypes = [typeof<'d1>; typeof<'d2>; typeof<'d3>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) (x.[2] :?> 'd3) |> box)
        }

    /// Creates a `Fuser` which fuses a production that:
    /// * Has `n` symbols underneath.
    /// * The type of the `n`th post-processed symbol of the production is `fType (n-1)`.
    /// * The type of the output is `tOut`
    /// * The function that does the fusion is `fFuser`.
    let createN n fType tOut fFuser =
        {
            InputTypes = List.init n fType
            OutputType = tOut
            TheFuser = fFuser
        }

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index + 1`th is significant and passed to the fusing function.
    let take1Of index n (fFuser: 'd -> 'r) =
        let fInputTypes =
            function
            | n when n = index -> typeof<'d>
            | _ -> typeof<obj>
        {
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index] :?> _) |> box)
        }

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index1 + 1`th and the `index2 + 1`th are significant and passed to the fusing function.
    let take2Of (index1, index2) n (fFuser: 'd1 -> 'd2 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> typeof<'d1>
            | n when n = index2 -> typeof<'d2>
            | _ -> typeof<obj>
        {
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) |> box)
        }

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index1 + 1`th, the `index2 + 1`th, and the `index3 + 1`th aresignificant and passed to the fusing function.
    let take3Of (index1, index2, index3) n (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> typeof<'d1>
            | n when n = index2 -> typeof<'d2>
            | n when n = index3 -> typeof<'d3>
            | _ -> typeof<obj>
        {
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) (x.[index3] :?> _) |> box)
        }
