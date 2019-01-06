// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

open Farkle.Grammar
open System

/// This type contains the logic to combine multiple symbols of one production into one arbitrary object.
/// These symbols are either transformed by a `Transformer` if they are terminals,
/// or, if they are productions, are the products of previous fusers.
type Fuser = internal {
    ProductionIndex: uint32
    InputTypes: Type option []
    OutputType: Type
    TheFuser: obj [] -> obj
}
with
    static member Create prod input output f =
        {
            ProductionIndex = prod
            InputTypes = input
            OutputType = output
            TheFuser = f
        }

/// Functions to create `Fuser`s.
module Fuser =

    /// Creates a `Fuser` that ignores all child productions and returns a constant object.
    let inline constant<'r> prod n (x: 'r) =
        Fuser.Create
            (uint32 prod)
            (Array.replicate n None)
            typeof<'r>
            (fun _ -> box x)

    /// Creates a `Fuser` that ~~fuses~~ maps a single-symbol production.
    let inline create1 prod (fFuser: 'd -> 'r) =
        Fuser.Create
            (uint32 prod)
            [|Some typeof<'d>|]
            typeof<'r>
            (fun x -> x.[0] :?> _ |> fFuser |> box)
        
    /// Creates a `Fuser` that takes a production with one item and returns it unmodified.
    let inline identity prod = create1 prod id

    /// Creates a `Fuser` that fuses a production with two symbols.
    let inline create2 prod (fFuser: 'd1 -> 'd2 -> 'r) =
        Fuser.Create
            (uint32 prod)
            [|Some typeof<'d1>; Some typeof<'d2>|]
            typeof<'r>
            (fun x -> fFuser (x.[0] :?> _) (x.[1] :?> _) |> box)

    /// Creates a `Fuser` that fuses a production with three symbols.
    let inline create3 prod (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        Fuser.Create
            (uint32 prod)
            [|Some typeof<'d1>; Some typeof<'d2>; Some typeof<'d3>|]
            typeof<'r>
            (fun x -> fFuser (x.[0] :?> _) (x.[1] :?> _) (x.[2] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production that:
    /// * Has `n` symbols underneath.
    /// * The type of the `n`th post-processed symbol of
    /// the production is determined from `fType (n-1)`.
    /// If the symbol does not carry any significant information
    /// (such as being a "+" in a mathematical expression), the function will return `None.
    /// * The type of the output is `tOut`
    /// * The function that does the fusion is `fFuser`.
    let inline createN prod n fType tOut fFuser =
        Fuser.Create
            (uint32 prod)
            (Array.init n fType)
            tOut
            fFuser

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index + 1`th is significant and passed to the fusing function.
    let inline take1Of prod index n (fFuser: 'd -> 'r) =
        let fInputTypes =
            function
            | n when n = index -> Some typeof<'d>
            | _ -> None
        Fuser.Create
            (uint32 prod)
            (Array.init n fInputTypes)
            typeof<'r>
            (fun x -> fFuser (x.[index] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index1 + 1`th and the `index2 + 1`th are significant and passed to the fusing function.
    let inline take2Of prod (index1, index2) n (fFuser: 'd1 -> 'd2 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> Some typeof<'d1>
            | n when n = index2 -> Some typeof<'d2>
            | _ -> None
        Fuser.Create
            (uint32 prod)
            (Array.init n fInputTypes)
            typeof<'r>
            (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production that haves `n` symbols underneath,
    /// but only the `index1 + 1`th, the `index2 + 1`th, and the `index3 + 1`th aresignificant and passed to the fusing function.
    let inline take3Of prod (index1, index2, index3) n (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> Some typeof<'d1>
            | n when n = index2 -> Some typeof<'d2>
            | n when n = index3 -> Some typeof<'d3>
            | _ -> None
        Fuser.Create
            (uint32 prod)
            (Array.init n fInputTypes)
            typeof<'r>
            (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) (x.[index3] :?> _) |> box)
