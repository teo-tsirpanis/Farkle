// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammar
open System

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this post-processor
/// will return from a grammar.</typeparam>
type PostProcessor<[<CovariantOut>] 'T> =
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    abstract Fuse: Production * obj[] -> obj
    inherit IO.ITransformer<Terminal>
