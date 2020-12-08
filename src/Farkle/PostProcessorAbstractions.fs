// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open System
open System.Collections.Generic
open System.Runtime.CompilerServices

/// <summary>A type that is passed to the transformers and
/// provides additional information about the terminal being transformed.</summary>
/// <remarks>It is explicitly implemented by <see cref="CharStream"/>
/// but casting it to this type is not recommended. Using this interface
/// outside the scope of a transformer is not supported either.</remarks>
type ITransformerContext =
    /// <summary>The position of the first character of the token.</summary>
    /// <remarks>In C# this property is shown as a read-write <c>ref</c> due
    /// to compiler limitations. It will be changed to a <c>readonly ref</c>
    /// at a future non-major release.</remarks>
    abstract StartPosition: inref<Position>
    /// <summary>The position of the last character of the token.</summary>
    /// <remarks>In C# this property is shown as a read-write <c>ref</c> due
    /// to compiler limitations. It will be changed to a <c>readonly ref</c>
    /// at a future non-major release.</remarks>
    abstract EndPosition: inref<Position>
    /// <summary>An associative collection of objects that
    /// can be indexed by a case-sensitive string.</summary>
    /// <remarks>The content of the object store is scoped to the
    /// <see cref="Farkle.IO.CharStream" /> the tokens come from.</remarks>
    abstract ObjectStore: IDictionary<string,obj>

/// The bridge between a character stream and the post-processor API.
type ITransformer<'sym> =
    /// <summary>Converts a terminal into an arbitrary object.</summary>
    abstract Transform: 'sym * ITransformerContext * ReadOnlySpan<char> -> [<Nullable(2uy)>] obj
