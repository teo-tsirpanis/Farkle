// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open System.Collections.Generic
open System.Runtime.CompilerServices

/// <summary>A type that is passed to the transformers and
/// provides additional information about the terminal being transformed.</summary>
/// <remarks>It is explicitly implemented by <see cref="CharStream"/>
/// but casting it to this type is not recommended. Using this interface
/// outside the scope of a transformer is not recommended either.</remarks>
type ITransformerContext =
    /// <summary>The position of the first character of the token.</summary>
    abstract StartPosition: Position
    /// <summary>The position of the last character of the token.</summary>
    abstract EndPosition: Position
    /// <summary>An associative collection of objects that
    /// can be indexed by a case-sensitive string.</summary>
    /// <remarks>The content of the object store is scoped to the
    /// <see cref="Farkle.IO.CharStream" /> the tokens come from.</remarks>
    abstract ObjectStore: IDictionary<string,obj>
