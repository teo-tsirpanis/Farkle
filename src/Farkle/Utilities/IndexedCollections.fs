// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open System.Collections
open System.Collections.Generic
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// A `SafeArray` of some "states", along with an initial one.
type StateTable<'a> =
    {
        /// The initial state. It should also be kept in the states as well.
        InitialState: 'a
        /// All the state table's states.
        States: ImmutableArray<'a>
    }
    with
        /// Gets the length of the state table.
        member x.Length = x.States.Length
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member x.Item (idx: uint32) = x.States.[int idx]
        interface IEnumerable with
            /// [omit]
            member x.GetEnumerator() = (x.States :> IEnumerable).GetEnumerator()
        interface IEnumerable<'a> with
            /// [omit]
            member x.GetEnumerator() = (x.States :> seq<_>).GetEnumerator()
        interface IReadOnlyCollection<'a> with
            /// [omit]
            member x.Count = x.Length
