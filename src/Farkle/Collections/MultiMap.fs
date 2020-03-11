// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections.Generic

/// An associative list that maps a key to multiple values.
type internal MultiMap<'key, 'value when 'key: equality> = private {
    Values: Dictionary<'key, HashSet<'value>>
}
with
    /// <summary>Adds the specified key-value pair.</summary>
    /// <returns>Whether the current multimap was modified.</returns>
    member x.Add(k, v) =
        match x.Values.TryGetValue(k) with
        | true, vs -> vs.Add(v)
        | false, _ ->
            let vs = HashSet()
            vs.Add(v) |> ignore
            x.Values.Add(k, vs)
            true

    /// <summary>Assiciates the specified values with the specified key.</summary>
    /// <returns>Whether the current multimap was modified.</returns>
    member x.AddRange(k, vs: _ IEnumerable) =
        match x.Values.TryGetValue(k) with
        | true, vs' ->
            let previousCount = vs'.Count
            vs'.UnionWith(vs)
            vs'.Count <> previousCount
        | false, _ ->
            x.Values.Add(k, HashSet vs)
            true

    /// Returns whether the given key-value pair exists in this collection.
    member x.Contains(k, v) =
        match x.Values.TryGetValue(k) with
        | true, vs -> vs.Contains(v)
        | false, _ -> false

    /// Returns whether all the given values are associated with the given key.
    member x.ContainsRange(k, vs) =
        match x.Values.TryGetValue(k) with
        | true, vs' -> vs'.IsSupersetOf(vs)
        | false, _ -> false

    /// <summary>Associates the elements that correspond to one key
    /// with the elements that correspond to another.</summary>
    /// <param name="kDest">The key to associate the values of the other key.</param>
    /// <param name="kSrc">The key whose values are to be associated with the other key.</param>
    /// <returns>Whether the current multimap was modified.</returns>
    /// <remarks>This association is not permanent. If a value is later associated
    /// with <paramref name="kSrc"/>, it won't be automatically associated with <paramref name="kDest"/>.</remarks>
    member x.Union(kDest, kSrc) =
        match x.Values.TryGetValue(kSrc), x.Values.TryGetValue(kDest) with
        | (true, vSrcs), (true, vDests) ->
            let previousCount = vDests.Count
            vDests.UnionWith(vSrcs)
            vDests.Count <> previousCount
        | (true, vSrcs), (false, _) -> x.Values.Add(kDest, HashSet vSrcs); true
        | (false, _), _ -> false

    /// Gets the sequence of values associated with the specified key.
    member x.Item k =
        match x.Values.TryGetValue(k) with
        | true, vs -> Seq.readonly vs
        | false, _ -> Seq.empty

/// Functions to create and manipulate `MultiMap`s.
module internal MultiMap =
    /// Creates a `MultiMap`.
    let create() = {Values = Dictionary()}
    /// Converts a `MultiMap` to an immutable collection.
    let freeze {Values = dict} = dict |> Seq.map (fun (KeyValue(k, vs)) -> k, set vs) |> Map.ofSeq
