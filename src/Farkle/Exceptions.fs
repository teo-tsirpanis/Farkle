// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammar
open Farkle.Parser
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// The base class for all exceptions
/// that might be thrown from Farkle.
type FarkleException([<Optional; DefaultParameterValue(null: string); Nullable(2uy)>] msg: string,
    [<Optional; Nullable(2uy)>] innerExn) =
    inherit exn(msg, innerExn)

/// An exception thrown by Farkle's parser on a snytax or lexical error.
type ParserException(error: ParserError) =
    inherit FarkleException(error.ToString())
    /// <summary>The <see cref="ParserError"/>
    /// object this exception holds.</summary>
    member _.Error = error

/// An exception thrown by Farkle when the post-processor throws
/// another exception. That exception is wrapped to this one.
type PostProcessorException private(msg, innerExn) =
    inherit FarkleException(msg, innerExn)
    new (term: Terminal, innerExn) =
        let message = sprintf "Exception while transforming terminal %O" term
        PostProcessorException(message, innerExn)
    new (prod: Production, innerExn) =
        let message = sprintf "Exception while fusing production %O" prod
        PostProcessorException(message, innerExn)

/// <summary>A parsing error that did not originate from
/// the parser, but from user code during post-processing.</summary>
/// <remarks>Exceptions of this type during post-processing
/// will be caught and rewrapped as <see cref="ParserException"/>s.
/// F# users can use the <c>error</c> or <c>errorf</c> functions
/// in <c>Farkle.Builder</c>.</remarks>
type ParserApplicationException(msg) = inherit FarkleException(msg)
