// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

/// This type contains the logic to "fuse" the multiple symbols of a production into an arbitrary object.
/// These symbols are either transformed by a `Transformer` if they are terminals,
/// or, if they are productions, are the products of previous fusers.
type Fuser = internal Fuser of uint32 * (obj[] -> obj)
with
    static member Create prod f = Fuser(prod, f)

/// Functions to create `Fuser`s.
module Fuser =

    /// Creates a `Fuser` that fuses a production with an arbitrary number and type of symbols.
    let inline create prod fFuser = Fuser.Create (uint32 prod) fFuser

    /// Creates a `Fuser` that ignores all child productions and returns a constant object.
    /// Caution must be exercised with regards to the type annotation of the constant.
    /// For example, `constant Production.Something None` would return an `obj option`.
    /// Instead, you should write something like this: `constant Production.Something (None: int option)`.
    let inline constant prod (x: 'r) = Fuser.Create (uint32 prod) (fun _ -> box x)

    /// Creates a `Fuser` that fuses a production from its first symbol.
    let inline create1 prod (fFuser: 'd -> 'r) =
        create prod (fun x -> x.[0] :?> _ |> fFuser |> box)

    /// Creates a `Fuser` that fuses a production by returning its first symbol unmodified.
    let inline identity prod = create1 prod id

    /// Creates a `Fuser` that fuses a production from its first two symbols.
    let inline create2 prod (fFuser: 'd1 -> 'd2 -> 'r) =
        create prod (fun x -> fFuser (x.[0] :?> _) (x.[1] :?> _) |> box)

    /// Creates a `Fuser` that fuses a production from its first three symbols.
    let inline create3 prod (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        create prod (fun x -> fFuser (x.[0] :?> _) (x.[1] :?> _) (x.[2] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production from one of its symbols.
    let inline take1Of prod index (fFuser: 'd -> 'r) =
        create prod (fun x -> fFuser (x.[index] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production from two of its symbols.
    let inline take2Of prod (index1, index2) (fFuser: 'd1 -> 'd2 -> 'r) =
        create prod (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) |> box)

    /// Creates a `Fuser` which fuses a production from three of its symbols.
    let inline take3Of prod (index1, index2, index3) (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        create prod (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) (x.[index3] :?> _) |> box)
