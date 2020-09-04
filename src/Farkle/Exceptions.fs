// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Parser

/// The base class for all exceptions
/// that might be thrown from Farkle.
type FarkleException(msg: string, innerExn) =
    inherit exn(msg, innerExn)
    new msg = FarkleException(msg, null)

/// An exception thrown by Farkle's parser on a snytax or lexical error.
type ParserException(error: ParserError) =
    inherit FarkleException(error.ToString(), null)
    /// <summary>The <see cref="ParserError"/>
    /// object this exception holds.</summary>
    member _.Error = error

/// <summary>A parsing error that did not originate from
/// the parser, but from user code during post-processing.</summary>
/// <remarks>Exceptions of this type during post-processing
/// will be caught and rewrapped as <see cref="ParserException"/>s.
/// F# users can use the <c>error</c> or <c>errorf</c> functions
/// in <c>Farkle.Builder</c>.</remarks>
type ParserApplicationException(msg) = inherit FarkleException(msg, null)
