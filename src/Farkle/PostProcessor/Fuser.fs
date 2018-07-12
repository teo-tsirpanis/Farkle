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
type Fuser<'TProduction> = internal {
    AcceptingProduction: 'TProduction
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
            UnexpectedASTStructure |> fail

/// Functions to create `Fuser`s.
module Fuser =
    /// Creates a `Fuser` that ~~fuses~~ maps a single-symbol production.
    let create1 prod (fFuser: 'd -> 'r) =
        {
            AcceptingProduction = prod
            InputTypes = [typeof<'d>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> x.[0] :?> 'd |> fFuser |> box)
        }

    /// Creates a `Fuser` that fuses a production with two symbols.
    let create2 prod (fFuser: 'd1 -> 'd2 -> 'r) =
        {
            AcceptingProduction = prod
            InputTypes = [typeof<'d1>; typeof<'d2>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) |> box)
        }

    /// Creates a `Fuser` that fuses a production with three symbols.
    let create3 prod (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        {
            AcceptingProduction = prod
            InputTypes = [typeof<'d1>; typeof<'d2>; typeof<'d3>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) (x.[2] :?> 'd3) |> box)
        }

    /// Creates a `Fuser` which fuses a production of type `prod` which:
    /// * Has `n` symbols underneath.
    /// * The type of the `n`th post-processed symbol of the production is `fType (n-1)`.
    /// * The type of the output is `tOut`
    /// * The function that does the fusion is `fFuser`.
    let createN prod n fType tOut fFuser =
        {
            AcceptingProduction = prod
            InputTypes = List.init n fType
            OutputType = tOut
            TheFuser = fFuser
        }

    /// Creates a `Fuser` which fuses a production of type `prod` that haves `n` symbols underneath,
    /// but only the `index + 1`th is significant and passed to the fusing function.
    let take1Of prod index n (fFuser: 'd -> 'r) =
        let fInputTypes =
            function
            | n when n = index -> typeof<'d>
            | _ -> typeof<obj>
        {
            AcceptingProduction = prod
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index] :?> _) |> box)
        }

    /// Creates a `Fuser` which fuses a production of type `prod` that haves `n` symbols underneath,
    /// but only the `index1 + 1`th and the `index2 + 1`th are significant and passed to the fusing function.
    let take2Of prod (index1, index2) n (fFuser: 'd1 -> 'd2 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> typeof<'d1>
            | n when n = index2 -> typeof<'d2>
            | _ -> typeof<obj>
        {
            AcceptingProduction = prod
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) |> box)
        }

    /// Creates a `Fuser` which fuses a production of type `prod` that haves `n` symbols underneath,
    /// but only the `index1 + 1`th, the `index2 + 1`th, and the `index3 + 1`th aresignificant and passed to the fusing function.
    let take3Of prod (index1, index2, index3) n (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) =
        let fInputTypes =
            function
            | n when n = index1 -> typeof<'d1>
            | n when n = index2 -> typeof<'d2>
            | n when n = index3 -> typeof<'d3>
            | _ -> typeof<obj>
        {
            AcceptingProduction = prod
            InputTypes = List.init n fInputTypes
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[index1] :?> _) (x.[index2] :?> _) (x.[index3] :?> _) |> box)
        }

/// This type contains the logic to combine multiple symbols of _all_ productions of a grammar into one.
// I can't use a map, because the compiler starts a "not-so-generic-code" rant.
type ProductionPostProcessor<'TProduction> = internal ProductionPostProcessor of ('TProduction -> Fuser<'TProduction> option)
with
    /// Fuses a list of objects that belong to the given production into one.
    /// Because productions always carry significant information up to the higher levels of parsing,
    /// the post-processing will fail accordingly if a proper `Fuser` is not found for the given production.
    member x.PostProcess prod data =
        x
        |> (fun (ProductionPostProcessor x) -> x prod)
        |> failIfNone UnexpectedASTStructure
        >>= (Fuser<'TProduction>.Fuse data)

/// Functions to create a `ProductionPostProcessor`.
module internal ProductionPostProcessor =

    /// Creates a `ProductionPostProcessor` that fuses all
    /// the productions that are recognized by the given `Fuser`s.
    /// In case many fusers recognize one production, the last one in order will be considered.
    let create fusers =
        let map = fusers |> Seq.map (fun x -> x.AcceptingProduction, x) |> Map.ofSeq
        let consistencyCheck =
            fusers
            |> Seq.groupBy (fun x -> x.AcceptingProduction)
            |> Seq.forall (snd >> Seq.map (fun x -> x.OutputType.AssemblyQualifiedName) >> set >> Set.count >> ((=) 1))
            // All different cases of a production must be fused to one type.
        if consistencyCheck then
            map.TryFind |> ProductionPostProcessor |> Ok
        else
            fail InconsistentProductionPostProcessor