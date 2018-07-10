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
    InputTypes: Type option list
    OutputType: Type
    TheFuser: obj list -> obj
}
with
    /// Fuses the many post-processed parts of a production into one.
    /// The types of the objects are optionally checked, depending on the kind of the fuser in question.
    static member Fuse x fus =
        let typeCheck = 
            x
            |> List.map (fun x -> x.GetType())
            |> List.zip fus.InputTypes
            |> List.forall (fun (x1, x2) -> x1 |> Option.map (fun x1 -> x1.AssemblyQualifiedName = x2.AssemblyQualifiedName) |> Option.defaultValue true)
        if typeCheck then
            fus.TheFuser x |> Ok
        else
            UnexpectedASTStructure |> fail

/// Functions to create `Fuser`s.
module Fuser =
    /// Creates a `Fuser` that ~~fuses~~ maps a single-symbol production.
    let create1 (fFuser: 'd -> 'r) prod =
        {
            AcceptingProduction = prod
            InputTypes = [Some typeof<'d>]
            OutputType = typeof<'r>
            TheFuser = (fun x -> x.[0] :?> 'd |> fFuser |> box)
        }

    let create2 (fFuser: 'd1 -> 'd2 -> 'r) prod =
        {
            AcceptingProduction = prod
            InputTypes = [typeof<'d1>; typeof<'d2>] |> List.map Some
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) |> box)
        }

    /// Creates a `Fuser` that fuses a production with three symbols.
    let create3 (fFuser: 'd1 -> 'd2 -> 'd3 -> 'r) prod =
        {
            AcceptingProduction = prod
            InputTypes = [typeof<'d1>; typeof<'d2>; typeof<'d3>] |> List.map Some
            OutputType = typeof<'r>
            TheFuser = (fun x -> fFuser (x.[0] :?> 'd1) (x.[1] :?> 'd2) (x.[2] :?> 'd3) |> box)
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
        |> failIfNone (UnexpectedASTStructure)
        >>= (Fuser<'TProduction>.Fuse data)

/// Functions to create a `ProductionPostProcessor`.
module ProductionPostProcessor =
    
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