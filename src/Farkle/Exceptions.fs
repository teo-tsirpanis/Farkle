// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammars
open Farkle.Parser
open System
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
        let message = sprintf "Exception while transforming terminal %O." term
        PostProcessorException(message, innerExn)
    new (prod: Production, innerExn) =
        let message = sprintf "Exception while fusing production %O." prod
        PostProcessorException(message, innerExn)

/// <summary>A parsing error that did not originate from
/// the parser, but from user code during post-processing.</summary>
/// <remarks>Exceptions of this type during post-processing
/// will be specially treated by the runtime Farkle API
/// as <see cref="ParseErrorType.UserError"/>s.
/// F# users can use the <c>error</c> or <c>errorf</c> functions
/// in <c>Farkle.Builder</c>.</remarks>
type ParserApplicationException private(msg, pos: Nullable<Position>) =
    inherit FarkleException(msg)
    /// Creates an exception with a custom error position.
    new (msg, pos) = ParserApplicationException(msg, Nullable pos)
    /// Creates an exception without a custom error position.
    new (msg) = ParserApplicationException(msg, Nullable())
    /// The optionally defined position of the error. If not set, it will
    /// default to the starting position of the token that was being created.
    member _.Position = &pos

namespace Farkle.IO

open Farkle
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
/// <summary>Extension methods to the <see cref="CharStream"/>
/// type that have to do with error reporting.</summary>
type CharStreamErrorReportingExtensions =
    /// <summary>Throws a <see cref="ParserApplicationException"/> at
    /// <paramref name="offset"/> characters after <paramref name="stream"/>'s
    /// current position.</summary>
    /// <param name="stream">The <see cref="CharStream"/> to work with.</param>
    /// <param name="offset">The offset from the character stream's current
    /// position to the point the error will be reported.</param>
    /// <param name="message">The exception's message</param>
    [<Extension>]
    #if MODERN_FRAMEWORK
    [<System.Diagnostics.CodeAnalysis.DoesNotReturn>]
    #endif
    static member FailAtOffset(stream: CharStream, offset, message) =
        let pos = stream.GetPositionAtOffset offset
        ParserApplicationException(message, pos)
        |> raise
        |> ignore
