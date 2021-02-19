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
/// outside the scope of a transformer is not recommended either.</remarks>
type ITransformerContext =
    /// The position of the first character of the token.
    abstract StartPosition: inref<Position>
    /// The position of the last character of the token.
    abstract EndPosition: inref<Position>
    /// <summary>An associative collection of objects that
    /// can be indexed by a case-sensitive string.</summary>
    /// <remarks>The content of the object store is scoped to the
    /// <see cref="Farkle.IO.CharStream" /> the tokens come from.</remarks>
    abstract ObjectStore: IDictionary<string,obj>

/// <summary>An interface that transforms tokens from a <see cref="CharStream"/>.
/// It is implemented by <see cref="PostProcessor{TResult}"/>s.</summary>
/// <typeparam name="TSymbol">A type whose objects identify the kind of the token
/// being transformed. In Farkle it is usually <see cref="Farkle.Grammar.Terminal"/>.</typeparam>
type ITransformer<'TSymbol> =
    /// <summary>Converts a token into an object.</summary>
    /// <param name="symbol">An object identifying the kind of the token.</param>
    /// <param name="context">A <see cref="ITransformerContext"/> object
    /// that provides more information about the token.</param>
    /// <param name="data">A read-only span of the token's characters.</param>
    /// <returns>An object. It can be <see langword="null"/>.</returns>
    abstract Transform: symbol: 'TSymbol * context: ITransformerContext * data: ReadOnlySpan<char>
        -> [<Nullable(2uy)>] obj
