// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.PostProcessor

/// An error in the post-processor.
type PostProcessError =
    /// The `Fuser` required more, less, or objects of different type than what it needs.
    | UnexpectedASTStructure
    /// The production post-processor encountered an unknown tyoe of production.
    /// Contrary to the terminal post-processor, the production post-processor _must_
    /// recognize _all_ productions, as all carry significant information.
    | UnknownProduction of string
    override x.ToString() =
        match x with
        | UnexpectedASTStructure -> "Unexpected AST structure; perhaps a node of it had either more or less leaves"
        | UnknownProduction x -> sprintf "A production of type %s is not recognized" x

/// This special type signifies that a terminal symbol was not recognized by the terminal post-processor.
/// The terminal post-processor _is_ allowed to fail. Some symbols like
/// the semicolons in programming languages carry no significant information up to the higher levels of the parser.
type UnknownTerminal = UnknownTerminal of string
